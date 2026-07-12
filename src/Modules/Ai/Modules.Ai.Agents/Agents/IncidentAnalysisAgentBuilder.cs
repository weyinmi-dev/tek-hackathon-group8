using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Prompts;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the incident-analysis agent (Phase 2 §5.1): network events → detected anomalies.
/// Toolless — the events are the input; it transforms rather than looks things up. It runs after
/// the deterministic threshold pre-filter in the analysis workflow (Phase 2 §7.2), so it only
/// reasons about the events the policy could not classify.
/// </summary>
public sealed class IncidentAnalysisAgentBuilder(IChatClient chatClient)
{
    public AIAgent Build() => chatClient.AsAIAgent(
        instructions: AgentPrompts.IncidentAnalysis,
        name: AgentNames.IncidentAnalysis);
}
