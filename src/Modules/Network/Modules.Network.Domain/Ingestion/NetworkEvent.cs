using SharedKernel;

namespace Modules.Network.Domain.Ingestion;

/// <summary>
/// One parsed row from an ingested network-ops log file. The aggregate root is the
/// owning <see cref="IngestionRun"/> — NetworkEvent has no independent lifecycle and
/// is not modified after Stage 1 (Parse).
/// </summary>
public sealed class NetworkEvent : Entity
{
    private NetworkEvent(
        Guid id,
        Guid ingestionRunId,
        DateTimeOffset occurredAt,
        string towerCode,
        int? signalPct,
        int? loadPct,
        int? latencyMs,
        string? rawStatus,
        string? rawPayload) : base(id)
    {
        IngestionRunId = ingestionRunId;
        OccurredAt = occurredAt;
        TowerCode = towerCode;
        SignalPct = signalPct;
        LoadPct = loadPct;
        LatencyMs = latencyMs;
        RawStatus = rawStatus;
        RawPayload = rawPayload;
    }

    private NetworkEvent() { }

    public Guid IngestionRunId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string TowerCode { get; private set; } = null!;
    public int? SignalPct { get; private set; }
    public int? LoadPct { get; private set; }
    public int? LatencyMs { get; private set; }
    public string? RawStatus { get; private set; }
    public string? RawPayload { get; private set; }

    public static NetworkEvent Create(
        Guid ingestionRunId,
        DateTimeOffset occurredAt,
        string towerCode,
        int? signalPct,
        int? loadPct,
        int? latencyMs,
        string? rawStatus,
        string? rawPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(towerCode);
        return new NetworkEvent(
            Guid.NewGuid(),
            ingestionRunId,
            occurredAt,
            towerCode.Trim().ToUpperInvariant(),
            signalPct,
            loadPct,
            latencyMs,
            rawStatus,
            rawPayload);
    }
}
