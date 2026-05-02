using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Modules.Ai.Application;
using Modules.Ai.Infrastructure;
using Modules.Alerts.Application;
using Modules.Alerts.Infrastructure;
using Modules.Analytics.Application;
using Modules.Analytics.Infrastructure;
using Modules.Energy.Application;
using Modules.Energy.Infrastructure;
using Modules.Identity.Application;
using Modules.Identity.Application.Authorization;
using Modules.Identity.Infrastructure;
using Modules.Identity.Infrastructure.Authentication;
using Modules.Network.Application;
using Modules.Network.Infrastructure;
using Serilog;
using ServiceDefaults;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddCors(opts => opts.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:3000", "http://localhost", "http://127.0.0.1:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Serialize all enums on the wire as their string names (e.g. "LocalUpload",
// "EngineeringSop") instead of their underlying integer values. Otherwise
// DocumentListItem.Source / Category come out as 0 / 3 in the documents UI,
// and any future enum DTO would silently inherit the same bug.
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
        ClockSkew = TimeSpan.FromSeconds(30)
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.RequireEngineer, p => p.RequireRole(Roles.Engineer, Roles.Manager, Roles.Admin));
    options.AddPolicy(Policies.RequireManager, p => p.RequireRole(Roles.Manager, Roles.Admin));
    options.AddPolicy(Policies.RequireAdmin, p => p.RequireRole(Roles.Admin));
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddIdentityApplication()
    .AddIdentityInfrastructure(builder.Configuration)
    .AddNetworkApplication()
    .AddNetworkInfrastructure(builder.Configuration)
    .AddAlertsApplication()
    .AddAlertsInfrastructure(builder.Configuration)
    .AddAnalyticsApplication()
    .AddAnalyticsInfrastructure(builder.Configuration)
    .AddEnergyApplication()
    .AddEnergyInfrastructure(builder.Configuration)
    .AddAiApplication()
    .AddAiInfrastructure(builder.Configuration);

// OSM geo-enrichment helper. Lives in Web.Api so it can reference both the AI
// module (which owns ISiteGeoLookup) and any module DTO it needs to attach a
// GeoSummary to. Endpoints inject GeoEnricher and call ForSitesAsync on the
// list they're about to serialize, satisfying the directive that the system
// "must use OSM MCP when location-based reasoning is required" — every site /
// alert / anomaly response now carries OSM-derived spatial context.
builder.Services.AddScoped<Web.Api.Endpoints.Geo.GeoEnricher>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

// All endpoints mount under /api — the public contract (POST /api/chat,
// GET /api/metrics, ...) is unversioned by design.
RouteGroupBuilder apiGroup = app.MapGroup("api");

app.MapEndpoints(apiGroup);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.ApplyMigrationsAsync();
    await app.SeedDataAsync();
}

app.UseCors();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseRequestContextLogging();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

await app.RunAsync();

public partial class Program;
