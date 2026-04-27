// TelcoPilot Aspire AppHost — local development orchestration.
//
// In local dev:  dotnet run --project src/AppHost   (boots Postgres, Redis, Web.Api, Next.js frontend)
// In production: docker-compose up                  (nginx + frontend + backend + postgres + redis)
//
// First-time frontend setup (one-time): cd frontend && npm install --legacy-peer-deps
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

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres", password: pgPassword, port: 5723)
    .WithDataVolume()
    .WithPgAdmin();

IResourceBuilder<PostgresDatabaseResource> db = postgres.AddDatabase("telcopilot");

IResourceBuilder<RedisResource> redis = builder
    .AddRedis("redis", port: 6379)
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<ProjectResource> webApi = builder.AddProject<Projects.Web_Api>("web-api")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(redis, "Cache")
    .WaitFor(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    // Forward user-secrets (Ai, Jwt) to the API process. The values themselves
    // are pulled from this AppHost's user-secrets store, never from source.
    .WithEnvironment("Ai__Provider",                 builder.Configuration["Ai:Provider"]                 ?? "Mock")
    .WithEnvironment("Ai__AzureOpenAi__Endpoint",    builder.Configuration["Ai:AzureOpenAi:Endpoint"]    ?? "")
    .WithEnvironment("Ai__AzureOpenAi__ApiKey",      builder.Configuration["Ai:AzureOpenAi:ApiKey"]      ?? "")
    .WithEnvironment("Ai__AzureOpenAi__Deployment",  builder.Configuration["Ai:AzureOpenAi:Deployment"]  ?? "gpt-4o-mini")
    .WithEnvironment("Jwt__Secret",                  builder.Configuration["Jwt:Secret"]                  ?? "dev-secret-replace-in-production-please-32chars-min");

// Next.js frontend. next.config.mjs reads BACKEND_INTERNAL_URL to rewrite /api/* to the API,
// so the browser hits a single origin and we don't need nginx in dev.
builder.AddNpmApp("frontend", "../../frontend", "dev")
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("BACKEND_INTERNAL_URL", webApi.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
