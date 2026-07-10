using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Prompts;
using Modules.Ai.Agents.Tools;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the conversational NOC copilot (Phase 2 §5.1). Constructed in the composition root with
/// the resolved <see cref="IChatClient"/> (Azure Responses in production, the deterministic client
/// offline) and the tool objects; <see cref="Build"/> is synchronous — async warm-up, if ever
/// needed, belongs in a hosted service, not a DI factory (Phase 2 Appendix A).
/// </summary>
public sealed class OperationsCopilotAgentBuilder(
    IChatClient chatClient,
    NetworkTools network,
    AlertTools alerts,
    EnergyTools energy,
    KnowledgeTools knowledge,
    DocumentTools documents)
{
    public AIAgent Build() => chatClient.AsAIAgent(
        instructions: AgentPrompts.OperationsCopilot,
        name: AgentNames.OperationsCopilot,
        tools:
        [
            AIFunctionFactory.Create(network.GetRegionMetrics),
            AIFunctionFactory.Create(network.GetTowerMetrics),
            AIFunctionFactory.Create(network.SearchTowers),
            AIFunctionFactory.Create(alerts.GetActiveOutages),
            AIFunctionFactory.Create(alerts.SearchAlarmHistory),
            AIFunctionFactory.Create(energy.GetEnergyKpis),
            AIFunctionFactory.Create(energy.DetectEnergyAnomalies),
            AIFunctionFactory.Create(energy.GetDieselTrace),
            AIFunctionFactory.Create(knowledge.QueryKnowledge),
            AIFunctionFactory.Create(documents.SearchDocuments),
        ]);
}
