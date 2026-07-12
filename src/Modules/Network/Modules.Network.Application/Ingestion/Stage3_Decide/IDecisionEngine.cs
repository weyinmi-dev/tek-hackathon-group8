using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Pure rule-based translator from AI output + current-state snapshots to a list of
/// concrete pipeline actions. No I/O, no DbContext, no Semantic Kernel — by construction
/// trivially unit-testable. Implementation lands in PR 3.
/// </summary>
public interface IDecisionEngine
{
    IReadOnlyList<PipelineAction> Decide(
        AiAnalysisResult ai,
        IReadOnlyList<AlertSnapshot> activeAlerts,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers);
}
