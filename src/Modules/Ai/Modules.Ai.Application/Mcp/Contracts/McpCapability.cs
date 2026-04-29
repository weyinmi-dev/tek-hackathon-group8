namespace Modules.Ai.Application.Mcp.Contracts;

/// <summary>
/// One callable function exposed by an MCP plugin — name, prose description, and
/// JSON-Schema-like parameter shape. Drives plugin discovery (<c>/api/mcp/plugins</c>),
/// the LLM tool registration step, and the operator's documentation in the UI.
/// </summary>
public sealed record McpCapability(
    string Name,
    string Description,
    IReadOnlyList<McpCapabilityParameter> Parameters);

public sealed record McpCapabilityParameter(
    string Name,
    string Type,
    string Description,
    bool IsRequired);
