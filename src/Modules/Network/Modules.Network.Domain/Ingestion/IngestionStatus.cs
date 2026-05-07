namespace Modules.Network.Domain.Ingestion;

/// <summary>
/// Lifecycle states of a single ingestion run. Transitions are validated by
/// <see cref="IngestionRun.TransitionTo"/>; only the moves listed in
/// <see cref="IngestionStatusTransitions"/> are legal.
/// </summary>
public enum IngestionStatus
{
    Pending = 0,
    Parsing = 1,
    Analyzing = 2,
    Deciding = 3,
    Persisting = 4,
    Projecting = 5,
    Completed = 6,
    Failed = 7
}

internal static class IngestionStatusTransitions
{
    public static bool CanTransition(IngestionStatus from, IngestionStatus to)
    {
        if (to == IngestionStatus.Failed)
        {
            return from is not IngestionStatus.Completed and not IngestionStatus.Failed;
        }

        return from switch
        {
            IngestionStatus.Pending => to == IngestionStatus.Parsing,
            IngestionStatus.Parsing => to == IngestionStatus.Analyzing,
            IngestionStatus.Analyzing => to == IngestionStatus.Deciding,
            IngestionStatus.Deciding => to == IngestionStatus.Persisting,
            IngestionStatus.Persisting => to == IngestionStatus.Projecting,
            IngestionStatus.Projecting => to == IngestionStatus.Completed,
            _ => false
        };
    }
}
