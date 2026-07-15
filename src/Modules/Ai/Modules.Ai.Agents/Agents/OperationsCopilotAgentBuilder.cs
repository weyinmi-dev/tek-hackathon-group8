using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Memory;
using Modules.Ai.Agents.Prompts;
using Modules.Ai.Agents.Tools;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the conversational NOC copilot (Phase 2 §5.1) — the tool-using agent that replaces the
/// Semantic Kernel orchestrator (Phase 3 M11). Constructed in the composition root with the resolved
/// <see cref="IChatClient"/> (Azure Responses in production, the deterministic client offline).
///
/// Unlike the M6 toolless build, this attaches the memory providers via
/// <see cref="ChatClientAgentOptions"/> so conversation history reaches the model (fixes Phase 1
/// §4.7): <see cref="PostgresChatHistoryProvider"/> loads/stores turns for the session's conversation
/// and <see cref="KnowledgeContextProvider"/> grounds each turn in the knowledge base.
/// </summary>
public sealed class OperationsCopilotAgentBuilder(
    IChatClient chatClient,
    PostgresChatHistoryProvider chatHistory,
    KnowledgeContextProvider knowledgeContext,
    NetworkTools network,
    AlertTools alerts,
    EnergyTools energy,
    KnowledgeTools knowledge,
    DocumentTools documents,
    GeoTools geo,
    SiteSyncTools sync)
{
    public AIAgent Build()
    {
        var options = new ChatClientAgentOptions
        {
            Name = AgentNames.OperationsCopilot,
            ChatOptions = new ChatOptions
            {
                Instructions = AgentPrompts.OperationsCopilot,
                Tools =
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
                    AIFunctionFactory.Create(geo.GetSiteGeoContext),
                    AIFunctionFactory.Create(geo.ClassifyRegion),

                    // Synchronised OSS snapshot state: a site's current condition, its telemetry
                    // history, and what any given upload actually changed. These answer "why is this
                    // site unhealthy", "what changed since yesterday", and "summarise this upload"
                    // from live state rather than from an embedded copy of it.
                    AIFunctionFactory.Create(sync.GetSiteDetail),
                    AIFunctionFactory.Create(sync.GetSiteTelemetry),
                    AIFunctionFactory.Create(sync.GetSyncReport),
                    AIFunctionFactory.Create(sync.ListRecentUploads),
                ],
            },
            ChatHistoryProvider = chatHistory,
            AIContextProviders = [knowledgeContext],
        };

        return new ChatClientAgent(chatClient, options);
    }
}
