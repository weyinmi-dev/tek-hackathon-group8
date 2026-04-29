namespace Modules.Ai.Application.Mcp.Contracts;

/// <summary>
/// One MCP plugin invocation. <c>PluginId</c> + <c>Capability</c> identify which
/// plugin function to run; <c>Arguments</c> is a free-form JSON dictionary the
/// plugin's adapter knows how to interpret. Keep this shape provider-agnostic —
/// it must work for in-process plugins, external MCP servers, and third-party
/// telco APIs alike.
/// </summary>
public sealed record McpInvocationRequest(
    string PluginId,
    string Capability,
    IReadOnlyDictionary<string, object?> Arguments,
    string? CorrelationId = null);
