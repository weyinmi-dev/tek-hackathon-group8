namespace SharedKernel;

/// <summary>
/// Coarse classification used by the RAG retriever to optionally narrow a
/// similarity search to a single corpus slice. Mirrors the data sources
/// listed in docs/instructions.md (incident reports, outage summaries,
/// engineering SOPs, tower performance, network diagnostics, alert history).
///
/// Lives in SharedKernel, alongside <see cref="DocumentSource"/>, because it is shared vocabulary
/// rather than AI-module state: the ingestion workflow, the endpoints, and the Energy indexer all
/// classify with it. Keeping it in Ai.Domain would force the agent layer to reference the entity
/// layer just to name a category — the dependency the Phase 2 §4.2 rules exist to prevent.
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
