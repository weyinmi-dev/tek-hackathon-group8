namespace Modules.Ai.Agents.Configuration;

/// <summary>
/// Stable keys under which the five MAF agents are registered as keyed singletons
/// (see Phase 2 design §5.1) and resolved by command handlers and workflow executors.
/// Centralised so renaming an agent is a single edit rather than a string hunt.
/// </summary>
public static class AgentNames
{
    /// <summary>Conversational NOC assistant. The only agent with a chat session.</summary>
    public const string OperationsCopilot = "operations-copilot";

    /// <summary>Network events → detected anomalies (structured output).</summary>
    public const string IncidentAnalysis = "incident-analysis";

    /// <summary>Anomaly + topology + prior incidents → cause hypothesis + confidence.</summary>
    public const string RootCause = "root-cause";

    /// <summary>Filename + text preview → relevance decision + extracted metadata.</summary>
    public const string DocumentIntake = "document-intake";

    /// <summary>Network log → topology delta (structured output).</summary>
    public const string Topology = "topology";
}
