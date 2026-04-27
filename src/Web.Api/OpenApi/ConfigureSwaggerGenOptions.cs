using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Web.Api.OpenApi;

/// <summary>
/// Single-document Swagger metadata. The original template wired in
/// API versioning; for the demo's flat <c>/api/*</c> contract we publish
/// one document.
/// </summary>
internal sealed class ConfigureSwaggerGenOptions : IConfigureNamedOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TelcoPilot API",
            Version = "v1",
            Description = "AI-Native Telco Operations — modular monolith backend."
        });

        // Endpoints follow the REPR pattern with nested Request/Response types
        // (e.g. Login+Request, Refresh+Request). Swashbuckle's default schemaId is
        // just Type.Name, so all nested "Request" types collide. Prefix nested types
        // with their declaring type to keep names readable and unique.
        options.CustomSchemaIds(t => t.IsNested
            ? $"{t.DeclaringType!.Name}{t.Name}"
            : t.Name);
    }

    public void Configure(string? name, SwaggerGenOptions options) => Configure(options);
}
