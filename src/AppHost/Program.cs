// TelcoPilot Aspire AppHost — local development orchestration.
//
// In local dev:  dotnet run --project src/AppHost   (boots Postgres, Redis, Web.Api, Next.js frontend)
// In production: docker-compose up                  (nginx + frontend + backend + postgres + redis)
//
// First-time frontend setup (one-time): cd frontend && npm install --legacy-peer-deps
//
// Persistence:
//   Both Postgres and Redis use ContainerLifetime.Persistent + named data volumes.
//   That means restarting the AppHost (or the host machine) does NOT wipe the
//   database, the seeded RAG corpus, the audit log, or any uploaded documents.
//
// Secrets (Azure OpenAI) come from .NET user-secrets keyed off this AppHost project's UserSecretsId.
// Set them with:
//   dotnet user-secrets --project src/AppHost set "Ai:Provider"               "AzureOpenAi"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Endpoint"   "https://<your>.openai.azure.com/"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:ApiKey"     "<your-key>"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Deployment" "gpt-4o-mini"
//   dotnet user-secrets --project src/AppHost set "Jwt:Secret"                "<32+char-random>"

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);


IResourceBuilder<ParameterResource> pgPassword =
    builder.AddParameter("postgres-password", "postgres123", secret: true);

// pgvector/pgvector:pg17 is the official postgres:17 image with the `vector` extension
// preinstalled — required by the AI module's RAG layer (CREATE EXTENSION IF NOT EXISTS vector).
//
#pragma warning disable S125 // Sections of code should not be commented out
                            // ContainerLifetime.Persistent + WithDataVolume keep the data across AppHost restarts;
                            // without it Aspire would tear down the container (and its anonymous volume) on stop.

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres", password: pgPassword, port: 5723)
    .WithImage("pgvector/pgvector", "pg17")
    .WithLifetime(ContainerLifetime.Persistent)
    // Distinct volume name from docker-compose's telcopilot-pg-data so Aspire (dev)
    // and compose (prod-shaped) don't fight over the same data dir.
    .WithDataVolume("telcopilot-pg-data-aspire")
    .WithPgAdmin();
#pragma warning restore S125 // Sections of code should not be commented out


IResourceBuilder<PostgresDatabaseResource> db = postgres.AddDatabase("telcopilot");

IResourceBuilder<RedisResource> redis = builder
    .AddRedis("redis", port: 6379)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("telcopilot-redis-data-aspire");

IResourceBuilder<ProjectResource> webApi = builder.AddProject<Projects.Web_Api>("web-api")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(redis, "Cache")
    .WaitFor(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    // Forward user-secrets (Ai, Jwt) to the API process. The values themselves
    // are pulled from this AppHost's user-secrets store, never from source.
    .WithEnvironment("Ai__Provider",                          builder.Configuration["Ai:Provider"]                          ?? "Mock")
    .WithEnvironment("Ai__AzureOpenAi__Endpoint",             builder.Configuration["Ai:AzureOpenAi:Endpoint"]             ?? "")
    .WithEnvironment("Ai__AzureOpenAi__ApiKey",               builder.Configuration["Ai:AzureOpenAi:ApiKey"]               ?? "")
    .WithEnvironment("Ai__AzureOpenAi__Deployment",           builder.Configuration["Ai:AzureOpenAi:Deployment"]           ?? "gpt-4o-mini")
    .WithEnvironment("Ai__AzureOpenAi__EmbeddingDeployment",  builder.Configuration["Ai:AzureOpenAi:EmbeddingDeployment"]  ?? "")
    .WithEnvironment("Ai__Rag__Enabled",                      builder.Configuration["Ai:Rag:Enabled"]                      ?? "true")
    .WithEnvironment("Ai__Rag__TopK",                         builder.Configuration["Ai:Rag:TopK"]                         ?? "5")
    .WithEnvironment("Ai__Rag__EmbeddingDimensions",          builder.Configuration["Ai:Rag:EmbeddingDimensions"]          ?? "1536")
    // Local document store root — relative to the AppHost working directory by default,
    // but operators can override via user-secrets (e.g. /var/telcopilot/documents).
    .WithEnvironment("Ai__Documents__LocalRoot",              builder.Configuration["Ai:Documents:LocalRoot"]              ?? "./.telcopilot/documents")
    .WithEnvironment("Jwt__Secret",                           builder.Configuration["Jwt:Secret"]                           ?? "dev-secret-replace-in-production-please-32chars-min");

// Next.js frontend. next.config.mjs reads BACKEND_INTERNAL_URL to rewrite /api/* to the API,
// so the browser hits a single origin and we don't need nginx in dev.
builder.AddNpmApp("frontend", "../../frontend", "dev")
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("BACKEND_INTERNAL_URL", webApi.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
