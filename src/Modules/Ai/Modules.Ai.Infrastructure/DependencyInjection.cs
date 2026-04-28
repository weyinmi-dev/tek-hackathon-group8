using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using Modules.Ai.Infrastructure.Database;
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

        services.AddDbContext<AiDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Ai))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChatLogRepository, ChatLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

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

            services.AddScoped<Kernel>(sp =>
            {
                IKernelBuilder kb = Kernel.CreateBuilder();
                kb.AddAzureOpenAIChatCompletion(
                    deploymentName: ai.AzureOpenAi.Deployment,
                    endpoint: normalizedEndpoint,
                    apiKey: ai.AzureOpenAi.ApiKey);

                Kernel k = kb.Build();
                k.Plugins.AddFromObject(sp.GetRequiredService<DiagnosticsSkill>(), nameof(DiagnosticsSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<OutageSkill>(),       nameof(OutageSkill));
                k.Plugins.AddFromObject(sp.GetRequiredService<RecommendationSkill>(), nameof(RecommendationSkill));
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
