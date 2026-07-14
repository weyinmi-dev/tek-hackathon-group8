namespace Modules.Network.Api;

public sealed record TowerSnapshot(
    string Code,
    string Name,
    string Region,
    int SignalPct,
    int LoadPct,
    string Status,
    string? Issue,
    double Latitude = 0,
    double Longitude = 0);

public sealed record RegionHealth(string Region, int TowerCount, int CriticalCount, int WarnCount, int AvgSignalPct);

public interface INetworkApi
{
    Task<IReadOnlyList<TowerSnapshot>> ListTowersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TowerSnapshot>> ListByRegionAsync(string region, CancellationToken cancellationToken = default);
    Task<TowerSnapshot?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionHealth>> GetRegionHealthAsync(CancellationToken cancellationToken = default);

    // ── Synchronised OSS snapshot state ───────────────────────────────────────
    // The read surface the Copilot answers site questions from. It lives on the cross-module port,
    // not on Network.Application, because the Ai module is forbidden from reaching into another
    // module's application layer — a rule the architecture tests enforce. The shapes below are
    // deliberately flat: they are a contract for other modules, not Network's internal DTOs.

    Task<SiteSyncState?> GetSiteSyncStateAsync(string siteCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SiteTelemetrySample>> GetSiteTelemetryAsync(
        string siteCode, int hours, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncRunSummary>> ListSyncRunsAsync(
        string? siteCode, int take, CancellationToken cancellationToken = default);

    Task<SyncRunSummary?> GetSyncRunAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);
}

/// <summary>A site's current condition as of its latest synchronised snapshot.</summary>
public sealed record SiteSyncState(
    string SiteCode,
    string Name,
    string Region,
    string Status,
    int SignalPct,
    int LoadPct,
    string? Issue,
    string? Provider,
    string? Vendor,
    IReadOnlyList<string> Technologies,
    int? HealthScore,
    DateTimeOffset? LastSynchronisedAt,
    DateTimeOffset? LastHeartbeat,
    double? TemperatureC,
    int? BatteryPct,
    int? GeneratorFuelPercent,
    bool? GridUp,
    bool? GeneratorRunning,
    int? LatencyMs,
    double? AvailabilityPercent,
    int? ConnectedUsers,
    IReadOnlyList<SiteAlarm> ActiveAlarms,
    IReadOnlyList<SiteEquipmentState> Equipment,
    IReadOnlyList<SiteTicket> OpenTickets);

public sealed record SiteAlarm(
    string AlarmId, string Severity, string? Category, string? Type, string? Status,
    DateTimeOffset? RaisedAt, string? Description);

public sealed record SiteEquipmentState(
    string EquipmentId, string Type, string? Model, string? Status, bool IsActive);

public sealed record SiteTicket(
    string TicketId, string Status, string? Priority, string? Issue,
    string? EngineerName, DateTimeOffset? CreatedAt, DateTimeOffset? EstimatedArrival);

/// <summary>One point in a site's reported history.</summary>
public sealed record SiteTelemetrySample(
    DateTimeOffset At,
    int? HealthScore,
    int? SignalPct,
    int? LoadPct,
    int? LatencyMs,
    double? TemperatureC,
    int? BatteryPct,
    int? DieselPct,
    bool? GridUp,
    double? DownlinkTrafficGb,
    int? ConnectedUsers,
    int OpenAlarmCount);

/// <summary>What one upload changed.</summary>
public sealed record SyncRunSummary(
    Guid IngestionRunId,
    string FileName,
    string Status,
    string SubmittedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationMs,
    int RecordsCreated,
    int RecordsUpdated,
    int RecordsArchived,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    IReadOnlyList<string> Warnings,
    string? FailureReason,
    IReadOnlyList<string> SiteCodes,
    string? Provider);
