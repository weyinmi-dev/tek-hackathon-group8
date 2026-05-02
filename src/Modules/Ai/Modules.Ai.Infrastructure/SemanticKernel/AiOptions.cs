namespace Modules.Ai.Infrastructure.SemanticKernel;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"AzureOpenAi" | "Mock". Defaults to Mock when AzureOpenAi creds are missing.</summary>
    public string Provider { get; init; } = "Mock";

    public AzureOpenAiOptions AzureOpenAi { get; init; } = new();

    public OsmOptions Osm { get; init; } = new();
}

public sealed class AzureOpenAiOptions
{
    public string Endpoint   { get; init; } = "";
    public string ApiKey     { get; init; } = "";
    public string Deployment { get; init; } = "gpt-4o-mini";

    /// <summary>
    /// Azure OpenAI deployment name for the embeddings model used by the RAG pipeline.
    /// Leave blank to fall back to the deterministic in-process hashing embedder
    /// (still indexes + retrieves, just with token-overlap relevance instead of true semantic recall).
    /// </summary>
    public string EmbeddingDeployment { get; init; } = "";
}

/// <summary>
/// OpenStreetMap geospatial provider. Wraps the same public APIs the
/// <see href="https://github.com/jagan-shanmugam/open-streetmap-mcp">jagan-shanmugam OSM MCP server</see>
/// uses internally — Nominatim for forward/reverse geocoding and Overpass for POI / infrastructure
/// queries — but in-process so we avoid running a Python sidecar. The plugin surface and capability
/// names mirror the upstream MCP server so any prompt or playbook authored against it stays compatible.
/// </summary>
public sealed class OsmOptions
{
    /// <summary>Nominatim base URL — defaults to the OSM-hosted endpoint.</summary>
    public string NominatimBaseUrl { get; init; } = "https://nominatim.openstreetmap.org";

    /// <summary>Overpass-API base URL — defaults to the main public mirror.</summary>
    public string OverpassBaseUrl  { get; init; } = "https://overpass-api.de/api/interpreter";

    /// <summary>
    /// User-Agent header. OSM's usage policy REQUIRES every request to identify the application
    /// — anonymous traffic gets blocked. Override per-deployment with a contact email.
    /// </summary>
    public string UserAgent { get; init; } = "TelcoPilot/1.0 (+https://telcopilot.example/contact)";

    /// <summary>Per-request HTTP timeout in seconds. Overpass queries can be slow; default is generous.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Cache TTL for OSM responses. Geographic facts change slowly — 24h gives the LLM stable
    /// answers and protects the public OSM endpoints from repeated identical queries.
    /// </summary>
    public int CacheHours { get; init; } = 24;

    /// <summary>Default search radius (metres) for nearby-infrastructure queries.</summary>
    public int DefaultRadiusMetres { get; init; } = 2000;
}
