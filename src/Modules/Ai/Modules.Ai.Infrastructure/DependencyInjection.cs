using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Npgsql;
using Application.Abstractions.Storage;
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
using Modules.Ai.Infrastructure.Mcp.Osm;
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
using Modules.Ai.Infrastructure.Pipeline;
using Modules.Ai.Infrastructure.Pipeline.Skills;
using Modules.Ai.Infrastructure.Pipeline.Validators;
using Modules.Ai.Infrastructure.SemanticKernel;
using Modules.Ai.Infrastructure.SemanticKernel.Skills;
using FluentValidation;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

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
            // NpgsqlDataSource.Bootstrap fires several pg_type metadata queries to resolve
            // the `vector` OID. In a Docker/Aspire environment the container can be marked
            // healthy (port open) before those queries complete within Npgsql's default 15 s
            // timeout, causing a spurious TimeoutException on first connection.
            var csb = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = 60,
                CommandTimeout = 120
            };
            var dsb = new NpgsqlDataSourceBuilder(csb.ConnectionString);
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
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<IManagedDocumentRepository, ManagedDocumentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Durability substrate (Phase 3 M5). Both are additive and idle until wired: the outbox
        // table stays empty until the async document pipeline writes to it (M9), and the
        // checkpoint store is consumed by the workflow host (M7). Registered here so the tables
        // are provisioned and the port resolves.
        services.AddScoped<Modules.Ai.Application.Workflows.IWorkflowCheckpointStore,
            Modules.Ai.Infrastructure.Checkpointing.WorkflowCheckpointStore>();
        services.AddHostedService<Modules.Ai.Infrastructure.Outbox.OutboxProcessor>();

        // Bind AiOptions through IOptions so the OSM layer (and anything else that needs the
        // sub-options) can consume it via constructor injection rather than re-binding the section.
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        AiOptions ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

        // Provider selection. AzureOpenAi requires endpoint + key + deployment.
        // Anything else (or missing creds) → deterministic mock orchestrator.
        bool useAzure =
            string.Equals(ai.Provider, "AzureOpenAi", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.Endpoint) &&
            !string.IsNullOrWhiteSpace(ai.AzureOpenAi.ApiKey);

        AddRagPipeline(services, rag, configuration);
        AddDocumentPipeline(services, configuration, useAzure);
        AddOsmLayer(services, configuration);
        AddMcpPluginLayer(services);

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
            services.AddScoped<EnergySkill>();
            services.AddScoped<OsmSkill>();

            services.AddScoped<Kernel>(sp =>
            {
                IKernelBuilder kb = Kernel.CreateBuilder();
                kb.AddAzureOpenAIChatCompletion(
                    deploymentName: ai.AzureOpenAi.Deployment,
                    endpoint: normalizedEndpoint,
                    apiKey: ai.AzureOpenAi.ApiKey);

                kb.Services.AddSingleton<PromptExecutionSettings>(new AzureOpenAIPromptExecutionSettings
                {
                    Temperature = 0.7,
                    ResponseFormat = "json_object"
                });

                sp.GetRequiredService<FileMcpClient>().AddToKernelBuilder(kb);

                Kernel k = kb.Build();
                k.Plugins.AddFromObject(sp.GetRequiredService<DiagnosticsSkill>(),    nameof(DiagnosticsSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<OutageSkill>(),         nameof(OutageSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<RecommendationSkill>(), nameof(RecommendationSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<KnowledgeSkill>(),      nameof(KnowledgeSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<InternalToolsSkill>(),  nameof(InternalToolsSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<EnergySkill>(),         nameof(EnergySkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<OsmSkill>(),            nameof(OsmSkill));
                return k;
            });
            services.AddScoped(sp => sp.GetRequiredService<Kernel>().GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
            services.AddScoped<ICopilotOrchestrator, SemanticKernelOrchestrator>();

            // Stage 2 — three SK skills + the wrapper that composes/validates/retries them.
            // Skills are NOT added to Kernel.Plugins because the analyzer invokes them
            // deterministically rather than letting the chat model auto-select them.
            services.AddScoped<INetworkAnomalySkill, SemanticKernelNetworkAnomalySkill>();
            services.AddScoped<INetworkOptimizationSkill, SemanticKernelNetworkOptimizationSkill>();
            services.AddScoped<INetworkEnergySkill, SemanticKernelNetworkEnergySkill>();
            services.AddScoped<INetworkTopologyMappingSkill, SemanticKernelNetworkTopologyMappingSkill>();
            services.AddSingleton<IValidator<AiAnalysisResult>, AiAnalysisResultValidator>();
            services.AddScoped<INetworkBatchAnalyzer, SemanticKernelNetworkBatchAnalyzer>();
        }
        else
        {
            services.AddScoped<ICopilotOrchestrator, MockCopilotOrchestrator>();

            // Heuristic Stage-2 fallback: lets the pipeline run end-to-end without an Azure
            // OpenAI key. Required by the demo + by deterministic integration tests.
            services.AddSingleton<INetworkBatchAnalyzer, HeuristicNetworkBatchAnalyzer>();
        }

        return services;
    }

    private static void AddRagPipeline(IServiceCollection services, RagOptions rag, IConfiguration configuration)
    {
        services.AddSingleton<IChunker>(_ => new RecursiveTextChunker(rag));
        services.AddScoped<IKnowledgeStore, PgVectorKnowledgeStore>();
        services.AddScoped<IRagIndexer, RagIndexer>();
        services.AddScoped<IRagRetriever, RagRetriever>();
        services.AddScoped<Modules.Ai.Infrastructure.Rag.Seed.LocalDocumentSeeder>();

        // Energy → knowledge bridge: a scoped indexer service + a hosted background
        // worker that re-syncs Site/Anomaly state every 5 minutes so the Copilot can
        // ground "why did Surulere consume more diesel yesterday" answers in fresh data.
        services.AddScoped<Modules.Ai.Infrastructure.Rag.Indexing.EnergyKnowledgeIndexer>();
        services.AddHostedService<Modules.Ai.Infrastructure.Rag.Indexing.EnergyKnowledgeIndexerService>();
        services.AddHostedService<Modules.Ai.Infrastructure.Rag.Seed.LocalDocumentSeederService>();
        services.AddHostedService<Modules.Ai.Infrastructure.Rag.Seed.LocalDocumentWatcherService>();

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
        services.AddScoped<IMcpPlugin, EnergyMcpPlugin>();
        services.AddScoped<IMcpPlugin, OsmMcpPlugin>();

        services.AddScoped<IMcpPluginRegistry, McpPluginRegistry>();
        services.AddScoped<IMcpInvoker, McpInvoker>();

        // FileMcpClient starts the @modelcontextprotocol/server-filesystem subprocess ONCE at
        // startup and caches the resulting KernelFunction list. The hosted service triggers
        // initialization; the kernel factory reads the cached list synchronously (no blocking,
        // no per-request process spawning). If npx or the package is unavailable, the FS plugin
        // is simply absent and nothing else breaks.
        services.AddSingleton<FileMcpClient>();
        services.AddHostedService<FileMcpClientInitializer>();
    }

    /// <summary>
    /// OpenStreetMap geospatial layer. Wraps the same public APIs the upstream
    /// <see href="https://github.com/jagan-shanmugam/open-streetmap-mcp">jagan-shanmugam OSM MCP server</see>
    /// uses (Nominatim + Overpass) but in-process, so we don't need a Python sidecar.
    ///
    /// HttpClient is registered through <see cref="IHttpClientFactory"/> so socket reuse,
    /// retries, and resilience handlers can be added later without touching the client.
    /// The cached decorator (<see cref="CachedOsmClient"/>) is what consumers receive — it
    /// fronts every primitive call with Redis so identical (lat, lon[, radius]) inputs hit
    /// the cache instead of OSM. <see cref="ISiteGeoLookup"/> bridges site-code → coordinates
    /// using the Network module's tower data, then composes the OSM-derived attributes
    /// (region type, accessibility score, nearest fuel station) per the directive's
    /// "compute once, reuse" rule.
    /// </summary>
    private static void AddOsmLayer(IServiceCollection services, IConfiguration configuration)
    {
        OsmOptions osm = configuration.GetSection($"{AiOptions.SectionName}:Osm").Get<OsmOptions>() ?? new OsmOptions();

        services.AddHttpClient<OsmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, osm.TimeoutSeconds));
            // OSM's tile / Nominatim usage policy REQUIRES a descriptive UA — anonymous
            // requests are throttled or blocked. Operators should set a contact email per
            // deployment via Ai:Osm:UserAgent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(osm.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        // OsmClient is the underlying transport; consumers get the cached decorator. Both
        // share the same HttpClient registered above (the cached layer simply delegates).
        services.AddScoped<IOsmClient>(sp =>
        {
            OsmClient inner = sp.GetRequiredService<OsmClient>();
            return new CachedOsmClient(
                inner,
                sp.GetRequiredService<global::Application.Abstractions.Caching.ICacheService>(),
                sp.GetRequiredService<IOptions<AiOptions>>());
        });

        services.AddScoped<ISiteGeoLookup, SiteGeoLookup>();

        // Pre-warm the OSM geo cache for every known tower in the background so the
        // first /api/alerts | /api/energy/sites request after a fresh deploy doesn't
        // blow GeoEnricher's batch budget against slow public Overpass.
        services.AddHostedService<GeoCacheWarmer>();
    }

    private static void AddDocumentPipeline(IServiceCollection services, IConfiguration configuration, bool useAzure)
    {
        DocumentsOptions docs = configuration.GetSection(DocumentsOptions.SectionName).Get<DocumentsOptions>() ?? new DocumentsOptions();
        services.AddSingleton(docs);

        // FileStagingService persists uploaded bytes under .telcopilot/uploads/ so they are
        // accessible to the @modelcontextprotocol/server-filesystem MCP server (FS plugin)
        // for copilot queries and to Stage-2 for raw-content enrichment of SK prompts.
        services.AddSingleton<IFileStagingService, FileStagingService>();

        // Local-disk provider — fully wired and the default destination for /documents uploads.
        services.AddSingleton<IDocumentStorageProvider, LocalDocumentStorageProvider>();

        // Cloud providers register as placeholders so the architecture can dispatch to them
        // and the UI can list them as "not yet connected". Swap each one out for a live SDK
        // adapter (Google.Apis.Drive.v3, Microsoft.Graph, Azure.Storage.Blobs, ...) when the
        // operator is ready to enable that source — no changes required to the ingestion
        // pipeline or the document handlers.
        services.AddSingleton<IDocumentStorageProvider, GoogleDriveDocumentStorageProvider>();
        // services.AddSingleton<IDocumentStorageProvider, OneDriveDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, SharePointDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, AzureBlobDocumentStorageProvider>();
        services.AddSingleton<IDocumentStorageProvider, WebLinkDocumentStorageProvider>();

        // Add a standard HttpClient for the cloud/web providers to use
        services.AddHttpClient<WebLinkDocumentStorageProvider>();

        services.AddSingleton<IDocumentStorageRegistry, DocumentStorageRegistry>();
        // DefaultDocumentTextExtractor dispatches on content type (PDF / text / unsupported).
        // The old plain-text-only extractor silently turned PDFs into garbage — see the
        // class comment for the full story. Singleton is fine; it's stateless apart from
        // the injected logger.
        services.AddSingleton<IDocumentTextExtractor, DefaultDocumentTextExtractor>();
        if (useAzure)
        {
            services.AddScoped<IDocumentValidator, AiDocumentValidator>();
        }
        else
        {
            services.AddScoped<IDocumentValidator, MockDocumentValidator>();
        }
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IDocumentSyncService, DocumentSyncService>();
    }

    internal static string NormalizeAzureOpenAiEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return raw;
        }

        if (uri.Scheme is not "https" and not "http")
        {
            throw new InvalidOperationException(
                $"Ai:AzureOpenAi:Endpoint has unsupported scheme '{uri.Scheme}'. " +
                $"Expected an https:// URL, e.g. https://<resource>.openai.azure.com/. " +
                $"If you copied an 'azureml://' or other internal URL from Azure ML, " +
                $"use the 'Keys and Endpoint' page of your Azure OpenAI resource instead.");
        }

        UriBuilder rooted = new(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port)
        {
            Path = "/",
        };
        return rooted.Uri.ToString();
    }
}
