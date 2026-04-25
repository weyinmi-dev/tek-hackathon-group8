// TelcoPilot Aspire AppHost — local development orchestration.
//
// In local dev:  dotnet run --project src/AppHost   (boots Postgres, Redis, Web.Api together)
// In production: docker-compose up                  (nginx + frontend + backend + postgres + redis)
//
// Secrets (Azure OpenAI) come from .NET user-secrets keyed off this AppHost project's UserSecretsId.
// Set them with:
//   dotnet user-secrets --project src/AppHost set "Ai:Provider"               "AzureOpenAi"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Endpoint"   "https://<your>.openai.azure.com/"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:ApiKey"     "<your-key>"
//   dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Deployment" "gpt-4o-mini"
//   dotnet user-secrets --project src/AppHost set "Jwt:Secret"                "<32+char-random>"

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> postgresPwd =
    builder.AddParameter("postgres-password", secret: true);

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres", password: postgresPwd, port: 5432)
    .WithDataVolume("telcopilot-pg-data")
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<PostgresDatabaseResource> db = postgres.AddDatabase("telcopilot");

IResourceBuilder<RedisResource> redis = builder
    .AddRedis("redis", port: 6379)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Web_Api>("web-api")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    // Forward user-secrets (Ai, Jwt) to the API process. The values themselves
    // are pulled from this AppHost's user-secrets store, never from source.
    .WithEnvironment("Ai__Provider",                 builder.Configuration["Ai:Provider"]                 ?? "Mock")
    .WithEnvironment("Ai__AzureOpenAi__Endpoint",    builder.Configuration["Ai:AzureOpenAi:Endpoint"]    ?? "")
    .WithEnvironment("Ai__AzureOpenAi__ApiKey",      builder.Configuration["Ai:AzureOpenAi:ApiKey"]      ?? "")
    .WithEnvironment("Ai__AzureOpenAi__Deployment",  builder.Configuration["Ai:AzureOpenAi:Deployment"]  ?? "gpt-4o-mini")
    .WithEnvironment("Jwt__Secret",                  builder.Configuration["Jwt:Secret"]                  ?? "dev-secret-replace-in-production-please-32chars-min");

await builder.Build().RunAsync();
