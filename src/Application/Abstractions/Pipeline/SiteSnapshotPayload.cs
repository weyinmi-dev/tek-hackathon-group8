using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Abstractions.Pipeline;

/// <summary>
/// Canonical, provider-neutral form of a full OSS site snapshot — the shape an MTN
/// (or future vendor) feed collapses into once parsed. This is the contract every
/// synchronisation-aware module reads; it deliberately lives beside
/// <see cref="AiAnalysisResult"/> in the shared pipeline abstractions so Network,
/// Energy, and Alerts can all consume it without referencing each other's domains.
///
/// It is a *reported state* document, not an instruction: nothing here is trusted to
/// name an action. The decision of what to create, update, or archive is made by the
/// Stage-3 planner from these facts, exactly as <see cref="AiAnalysisResult"/> is run
/// through <c>DefaultDecisionEngine</c>. Vendor-supplied verdicts (risk scores,
/// recommended actions) are intentionally NOT modelled — allowing them would let an
/// upstream feed bypass our own thresholds and become the source of truth for alerts.
/// </summary>
public sealed record SiteSnapshotPayload(
    string RequestId,
    string Provider,
    string Environment,
    DateTimeOffset GeneratedAt,
    SnapshotSite Site,

    // The wire names carry a "Metrics" suffix the domain has no use for. Map explicitly rather
    // than renaming the properties — the shorter names are what every consumer reads.
    [property: JsonPropertyName("environmentalMetrics")] SnapshotEnvironmentalMetrics? Environmental,
    [property: JsonPropertyName("performanceMetrics")] SnapshotPerformanceMetrics? Performance,

    IReadOnlyList<SnapshotAlarm> ActiveAlarms,
    SnapshotMaintenance? Maintenance)
{
    /// <summary>
    /// Bumped when the canonical shape changes in a way that alters how a stored
    /// snapshot deserialises. Persisted per record so a future reader can tell which
    /// contract wrote a given row.
    /// </summary>
    public const int CurrentVersion = 1;

    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public static SiteSnapshotPayload? Deserialize(string json) =>
        JsonSerializer.Deserialize<SiteSnapshotPayload>(json, SerializerOptions);
}

public sealed record SnapshotSite(
    string SiteId,
    string SiteCode,
    string SiteName,
    string Region,
    string? Cluster,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Technology,
    string? Vendor,
    string? Status,
    int? HealthScore,
    DateOnly? CommissionedDate,
    DateTimeOffset? LastHeartbeat,
    IReadOnlyList<SnapshotEquipment> Equipment);

public sealed record SnapshotEquipment(
    string EquipmentId,
    string Type,
    string? Model,
    string? Status);

public sealed record SnapshotEnvironmentalMetrics(
    double? Temperature,
    double? Humidity,
    double? BatteryVoltage,
    int? GeneratorFuelPercent,
    bool? GeneratorRunning,
    bool? MainPowerAvailable,
    string? AirConditionerStatus,
    bool? DoorOpen,
    bool? SmokeDetected);

public sealed record SnapshotPerformanceMetrics(
    string? MeasurementInterval,
    DateTimeOffset? CapturedAt,
    double? AvailabilityPercent,
    int? ConnectedUsers,
    int? ActiveVoiceCalls,
    int? ActiveDataSessions,
    double? DownlinkTrafficGb,
    double? UplinkTrafficGb,
    double? AverageDownlinkMbps,
    double? AverageUplinkMbps,
    double? PacketLossPercent,
    int? LatencyMs,
    double? CallSetupSuccessRate,
    double? CallDropRate,
    double? HandoverSuccessRate,
    int? CellUtilizationPercent,
    IReadOnlyList<SnapshotKpi> Kpis);

public sealed record SnapshotKpi(string Name, double Value, string? Unit);

public sealed record SnapshotAlarm(
    string AlarmId,
    string Severity,
    string? Category,
    string? Type,
    string? Status,
    string? Source,
    DateTimeOffset? RaisedAt,
    string? Description);

public sealed record SnapshotMaintenance(
    DateOnly? LastMaintenanceDate,
    DateOnly? NextScheduledMaintenance,
    IReadOnlyList<SnapshotTicket> OpenTickets,
    IReadOnlyList<SnapshotMaintenanceHistory> MaintenanceHistory);

public sealed record SnapshotTicket(
    string TicketId,
    string? Priority,
    string? Status,
    SnapshotEngineer? AssignedEngineer,
    string? Issue,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? EstimatedArrival);

public sealed record SnapshotEngineer(string EngineerId, string Name);

public sealed record SnapshotMaintenanceHistory(
    string TicketId,
    DateTimeOffset? CompletedAt,
    string? Engineer,
    string? Action);
