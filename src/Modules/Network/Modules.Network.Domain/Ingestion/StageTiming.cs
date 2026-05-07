namespace Modules.Network.Domain.Ingestion;

public sealed record StageTiming(
    IngestionStatus Stage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    string? FailureReason)
{
    public TimeSpan Elapsed => CompletedAt - StartedAt;
}
