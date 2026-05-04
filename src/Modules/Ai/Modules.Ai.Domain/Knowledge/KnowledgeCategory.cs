namespace Modules.Ai.Domain.Knowledge;

/// <summary>
/// Coarse classification used by the RAG retriever to optionally narrow a
/// similarity search to a single corpus slice. Mirrors the data sources
/// listed in docs/instructions.md (incident reports, outage summaries,
/// engineering SOPs, tower performance, network diagnostics, alert history).
/// </summary>
public enum KnowledgeCategory
{
    IncidentReport = 0,
    OutageSummary = 1,
    NetworkDiagnostic = 2,
    EngineeringSop = 3,
    TowerPerformance = 4,
    AlertHistory = 5,
    // Energy module: per-site fuel/battery/solar narratives so the Copilot can answer
    // "why did Surulere consume more diesel yesterday" with grounded historical context.
    EnergySiteSnapshot = 6,
    EnergyAnomaly = 7,
}
