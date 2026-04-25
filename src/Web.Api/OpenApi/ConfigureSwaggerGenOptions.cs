using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Web.Api.OpenApi;

/// <summary>
/// Single-document Swagger metadata. The original template wired in
/// API versioning; for the demo's flat <c>/api/*</c> contract we publish
/// one document.
/// </summary>
internal sealed class ConfigureSwaggerGenOptions : IConfigureNamedOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options) =>
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TelcoPilot API",
            Version = "v1",
            Description = "AI-Native Telco Operations — modular monolith backend."
        });

    public void Configure(string? name, SwaggerGenOptions options) => Configure(options);
}
