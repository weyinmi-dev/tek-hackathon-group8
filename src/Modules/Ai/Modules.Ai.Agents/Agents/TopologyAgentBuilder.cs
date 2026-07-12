using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Prompts;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the topology agent (Phase 2 §5.1): a network log → topology delta (status changes and
/// metric updates). Toolless extraction, invoked by the analysis workflow (Phase 2 §7.2).
/// </summary>
public sealed class TopologyAgentBuilder(IChatClient chatClient)
{
    public AIAgent Build() => chatClient.AsAIAgent(
        instructions: AgentPrompts.Topology,
        name: AgentNames.Topology);
}
