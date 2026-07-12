using Application.Abstractions.Messaging;
using Modules.Energy.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools;

// Canonical Energy tool queries (Phase 2 §6.2) over IEnergyApi.

/// <summary>get_energy_kpis — fleet-wide energy/power KPIs.</summary>
public sealed record GetEnergyKpisQuery() : IQuery<EnergyKpiSnapshot>;

internal sealed class GetEnergyKpisQueryHandler(IEnergyApi energy)
    : IQueryHandler<GetEnergyKpisQuery, EnergyKpiSnapshot>
{
    public async Task<Result<EnergyKpiSnapshot>> Handle(GetEnergyKpisQuery request, CancellationToken cancellationToken)
        => Result.Success(await energy.GetKpisAsync(cancellationToken));
}

/// <summary>detect_energy_anomalies — the most recent energy anomalies (fuel theft, battery, diesel).</summary>
public sealed record DetectEnergyAnomaliesQuery(int Take = 50) : IQuery<IReadOnlyList<AnomalySnapshot>>;

internal sealed class DetectEnergyAnomaliesQueryHandler(IEnergyApi energy)
    : IQueryHandler<DetectEnergyAnomaliesQuery, IReadOnlyList<AnomalySnapshot>>
{
    public async Task<Result<IReadOnlyList<AnomalySnapshot>>> Handle(DetectEnergyAnomaliesQuery request, CancellationToken cancellationToken)
        => Result.Success(await energy.ListAnomaliesAsync(request.Take, cancellationToken));
}

/// <summary>get_diesel_trace — diesel-level trace for a site over the last N hours.</summary>
public sealed record GetDieselTraceQuery(string SiteCode, int Hours = 24) : IQuery<IReadOnlyList<DieselTracePoint>>;

internal sealed class GetDieselTraceQueryHandler(IEnergyApi energy)
    : IQueryHandler<GetDieselTraceQuery, IReadOnlyList<DieselTracePoint>>
{
    public async Task<Result<IReadOnlyList<DieselTracePoint>>> Handle(GetDieselTraceQuery request, CancellationToken cancellationToken)
        => Result.Success(await energy.GetSiteDieselTraceAsync(request.SiteCode, request.Hours, cancellationToken));
}
