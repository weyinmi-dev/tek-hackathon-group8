using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Npgsql;
using Modules.Ai.Application.Mcp.Clients;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Ai.Application.Mcp.Registry;
using Modules.Ai.Application.Rag;
using Modules.Ai.Application.Rag.Chunking;
using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Application.Rag.Embeddings;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Application.Rag.Retrievers;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Application.Rag.Stores;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database;
using Modules.Ai.Infrastructure.Mcp.Clients;
using Modules.Ai.Infrastructure.Mcp.Plugins;
using Modules.Ai.Infrastructure.Mcp.Registry;
using Modules.Ai.Infrastructure.Rag.Chunking;
using Modules.Ai.Infrastructure.Rag.Embeddings;
using Modules.Ai.Infrastructure.Rag.Indexing;
using Modules.Ai.Infrastructure.Rag.Ingestion;
using Modules.Ai.Infrastructure.Rag.Retrievers;
using Modules.Ai.Infrastructure.Rag.Storage;
using Modules.Ai.Infrastructure.Rag.Storage.Providers;
using Modules.Ai.Infrastructure.Rag.Stores;
using Modules.Ai.Infrastructure.Repositories;
using Modules.Ai.Infrastructure.SemanticKernel;
using Modules.Ai.Infrastructure.SemanticKernel.Skills;
using SharedKernel;

namespace Modules.Ai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("telcopilot");
        Ensure.NotNullOrEmpty(connectionString);

        // RAG options first — the DbContext needs the embedding dimensions to size the vector column.
        RagOptions rag = configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions();
        services.AddSingleton(rag);

        // Build an explicit NpgsqlDataSource with the pgvector type plugin registered. This is
        // required: the connection-string overload of UseNpgsql() creates an internal data source
        // that does NOT process EF-level plugins for parameter serialization, so writing a
        // Pgvector.Vector parameter would throw "no NpgsqlDbType". UseVector() at the data-source
        // level wires up Vector ↔ vector(N) for both reads and writes.
        //
        // The data source is built lazily on first OpenConnection — by which time
        // EnsurePgVectorExtensionAsync has already created the `vector` extension via a
        // separate raw connection, so type-OID lookup succeeds.
        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var dsb = new NpgsqlDataSourceBuilder(connectionString);
            dsb.UseVector();
            return dsb.Build();
        });

        services.AddDbContext<AiDbContext>((sp, opts) => opts
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npg =>
            {
                npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Ai);
                npg.UseVector();
            })
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChatLogRepository, ChatLogRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<IManagedDocumentRepository, ManagedDocumentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        AddRagPipeline(services, rag, configuration);
        AddDocumentPipeline(services, configuration);
        AddMcpPluginLayer(services);

        AiOptions ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

        // Provider selection. AzureOpenAi requires endpoint + key + deployment.
        // Anything else (or missing creds) → deterministic mock orchestrator.
        bool useAzure =
            string.Equals(ai.Provider, "AzureOpenAi", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.Endpoint) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.ApiKey);

        if (useAzure)
        {
            // SK's AzureOpenAIClient appends "/openai/deployments/{deployment}/chat/completions"
            // to whatever endpoint we pass, so it must be the resource root. Operators commonly
            // paste a Foundry-style URL ending in "/api/projects/<p>/openai/v1/responses"; strip
            // it back to "<scheme>://<host>/" so request URIs come out correct.
            string normalizedEndpoint = NormalizeAzureOpenAiEndpoint(ai.AzureOpenAi.Endpoint);

            services.AddScoped<DiagnosticsSkill>();
            services.AddScoped<OutageSkill>();
            services.AddScoped<RecommendationSkill>();
            services.AddScoped<KnowledgeSkill>();
            services.AddScoped<InternalToolsSkill>();

            services.AddScoped<Kernel>(sp =>
            {
                IKernelBuilder kb = Kernel.CreateBuilder();
                kb.AddAzureOpenAIChatCompletion(
                    deploymentName: ai.AzureOpenAi.Deployment,
                    endpoint: normalizedEndpoint,
                    apiKey: ai.AzureOpenAi.ApiKey);

                Kernel k = kb.Build();
                k.Plugins.AddFromObject(sp.GetRequiredService<DiagnosticsSkill>(),    nameof(DiagnosticsSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<OutageSkill>(),         nameof(OutageSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<RecommendationSkill>(), nameof(RecommendationSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<KnowledgeSkill>(),      nameof(KnowledgeSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<InternalToolsSkill>(),  nameof(InternalToolsSkill));
                return k;
            });
            services.AddScoped(sp => sp.GetRequiredService<Kernel>().GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
            services.AddScoped<ICopilotOrchestrator, SemanticKernelOrchestrator>();
        }
        else
        {
            services.AddScoped<ICopilotOrchestrator, MockCopilotOrchestrator>();
        }

        return services;
    }

    private static void AddRagPipeline(IServiceCollection services, RagOptions rag, IConfiguration configuration)
    {
        services.AddSingleton<IChunker>(_ => new RecursiveTextChunker(rag));
        services.AddScoped<IKnowledgeStore, PgVectorKnowledgeStore>();
        services.AddScoped<IRagIndexer, RagIndexer>();
        services.AddScoped<IRagRetriever, RagRetriever>();

        AiOptions ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        bool useAzureEmbeddings =
            string.Equals(ai.Provider, "AzureOpenAi", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.Endpoint) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.ApiKey) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.EmbeddingDeployment);

        if (useAzureEmbeddings)
        {
            string normalizedEndpoint = NormalizeAzureOpenAiEndpoint(ai.AzureOpenAi.Endpoint);
            string deployment = ai.AzureOpenAi.EmbeddingDeployment;
            int dim = rag.EmbeddingDimensions;

            services.AddSingleton(_ => new AzureOpenAIClient(
                new Uri(normalizedEndpoint),
                new ApiKeyCredential(ai.AzureOpenAi.ApiKey)));

            services.AddSingleton<IEmbeddingGenerator>(sp => new AzureOpenAiEmbeddingGenerator(
                sp.GetRequiredService<AzureOpenAIClient>(),
                deployment,
                dim,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureOpenAiEmbeddingGenerator>>()));
        }
        else
        {
            // Offline / Mock mode — deterministic hashing embedder. RAG still works end-to-end,
            // just with token-overlap relevance instead of true semantic recall.
            services.AddSingleton<IEmbeddingGenerator>(_ => new HashingEmbeddingGenerator(rag.EmbeddingDimensions));
        }
    }

    private static void AddMcpPluginLayer(IServiceCollection services)
    {
        // Built-in plugins are scoped because they consume scoped module APIs (DbContext-bound).
        // Add new plugins by registering an additional IMcpPlugin — they'll show up in the
        // registry and the discovery endpoint automatically.
        services.AddScoped<IMcpPlugin, NetworkMcpPlugin>();
        services.AddScoped<IMcpPlugin, AlertsMcpPlugin>();

        services.AddScoped<IMcpPluginRegistry, McpPluginRegistry>();
        services.AddScoped<IMcpInvoker, McpInvoker>();
    }

    private static void AddDocumentPipeline(IServiceCollection services, IConfiguration configuration)
    {
        DocumentsOptions docs = configuration.GetSection(DocumentsOptions.SectionName).Get<DocumentsOptions>() ?? new DocumentsOptions();
        services.AddSingleton(docs);

        // Local-disk provider — fully wired and the default destination for /documents uploads.
        services.AddSingleton<IDocumentStorageProvider, LocalDocumentStorageProvider>();

        // Cloud providers register as placeholders so the architecture can dispatch to them
        // and the UI can list them as "not yet connected". Swap each one out for a live SDK
        // adapter (Google.Apis.Drive.v3, Microsoft.Graph, Azure.Storage.Blobs, ...) when the
        // operator is ready to enable that source — no changes required to the ingestion
        // pipeline or the document handlers.
        services.AddSingleton<IDocumentStorageProvider, GoogleDriveDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, OneDriveDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, SharePointDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, AzureBlobDocumentStorageProvider>();

        services.AddSingleton<IDocumentStorageRegistry, DocumentStorageRegistry>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextDocumentExtractor>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
    }

    internal static string NormalizeAzureOpenAiEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return raw;
        }

        UriBuilder rooted = new(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port)
        {
            Path = "/",
        };
        return rooted.Uri.ToString();
    }
}
