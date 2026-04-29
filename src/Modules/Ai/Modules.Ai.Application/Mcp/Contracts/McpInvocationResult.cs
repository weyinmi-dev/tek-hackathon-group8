namespace Modules.Ai.Application.Mcp.Contracts;

/// <summary>
/// Structured response from an MCP plugin invocation. <c>Output</c> is the
/// serializable payload (string, dict, list — whatever the plugin returns);
/// <c>Diagnostics</c> carries timing and provenance for the audit trail and
/// the AI synthesis step.
/// </summary>
public sealed record McpInvocationResult(
    string PluginId,
    string Capability,
    bool IsSuccess,
    object? Output,
    string? Error,
    long DurationMs,
    string? CorrelationId = null);
