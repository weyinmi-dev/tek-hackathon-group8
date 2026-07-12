using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Prompts;
using Modules.Ai.Agents.Tools;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the root-cause agent (Phase 2 §5.1): given an anomaly plus context, determine the most
/// likely cause. Uses the tower and knowledge tools to ground the hypothesis in live state and
/// prior incidents.
/// </summary>
public sealed class RootCauseAgentBuilder(IChatClient chatClient, NetworkTools network, KnowledgeTools knowledge)
{
    public AIAgent Build() => chatClient.AsAIAgent(
        instructions: AgentPrompts.RootCause,
        name: AgentNames.RootCause,
        tools:
        [
            AIFunctionFactory.Create(network.GetTowerMetrics),
            AIFunctionFactory.Create(knowledge.QueryKnowledge),
        ]);
}
