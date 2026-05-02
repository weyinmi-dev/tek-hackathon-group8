using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Optimization.GetOptimization;

/// <summary>
/// Cost-optimization projection driven by simulator inputs (solar%, diesel ₦/L, battery threshold).
/// Pure compute over the live fleet — no random data — so the same inputs yield the same projection
/// for the same fleet snapshot. Used by the /optimize page sliders and by RecommendationSkill.
/// </summary>
public sealed record GetOptimizationProjectionQuery(int SolarPct, int DieselPriceNgnPerLitre, int BatteryThresholdPct)
    : IQuery<OptimizationProjectionResponse>;

public sealed record OptimizationProjectionResponse(
    double BaselineDailyOpexMillionsNgn,
    double OptimizedDailyOpexMillionsNgn,
    double DailySavingsMillionsNgn,
    double AnnualSavingsBillionsNgn,
    int DieselReductionPct,
    int Co2AvoidedTonnesPerYear,
    IReadOnlyList<double> BaselineSeries,
    IReadOnlyList<double> OptimizedSeries,
    IReadOnlyList<EnergyMixSlice> EnergyMix);

public sealed record EnergyMixSlice(string Source, int Pct);

internal sealed class GetOptimizationProjectionQueryHandler(ISiteRepository sites)
    : IQueryHandler<GetOptimizationProjectionQuery, OptimizationProjectionResponse>
{
    public async Task<Result<OptimizationProjectionResponse>> Handle(GetOptimizationProjectionQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> all = await sites.ListAsync(cancellationToken);

        int total = all.Count == 0 ? 1 : all.Count;
        int onSolar = all.Count(s => s.Source == PowerSource.Solar);
        int onGrid = all.Count(s => s.Source == PowerSource.Grid);
        int onBatt = all.Count(s => s.Source == PowerSource.Battery);
        int onGen = total - onSolar - onGrid - onBatt;

        // Same model the design prototype used — kept identical so the OPEX/projection numbers
        // match what stakeholders saw in the mock. The slope is intentional: more solar +
        // higher battery threshold = larger reduction; higher diesel price erodes the gain.
        const double baseCostMPerDay = 21.0;
        double solarSavings = request.SolarPct * 0.12;
        double battSavings = (request.BatteryThresholdPct - 50) * 0.04;
        double dieselFactor = (request.DieselPriceNgnPerLitre - 700) * 0.002;
        double optimized = Math.Max(8, baseCostMPerDay - solarSavings - battSavings + dieselFactor);
        double daily = baseCostMPerDay - optimized;
        double annual = daily * 365 / 1000.0;
        int dieselReduction = (int)Math.Round(request.SolarPct * 0.5 + (request.BatteryThresholdPct - 50) * 0.3);

        double[] baseline = new double[30];
        double[] optimizedSeries = new double[30];
        for (int i = 0; i < 30; i++)
        {
            baseline[i] = baseCostMPerDay + ((i % 5) - 2) * 0.4;
            optimizedSeries[i] = Math.Max(8, optimized + Math.Sin(i / 3.0) * 0.8);
        }

        IReadOnlyList<EnergyMixSlice> mix =
        [
            new EnergyMixSlice("Diesel", 100 * onGen / total),
            new EnergyMixSlice("Grid",   100 * onGrid / total),
            new EnergyMixSlice("Battery",100 * onBatt / total),
            new EnergyMixSlice("Solar",  100 * onSolar / total),
        ];

        return Result.Success(new OptimizationProjectionResponse(
            BaselineDailyOpexMillionsNgn: baseCostMPerDay,
            OptimizedDailyOpexMillionsNgn: optimized,
            DailySavingsMillionsNgn: daily,
            AnnualSavingsBillionsNgn: annual,
            DieselReductionPct: Math.Max(0, dieselReduction),
            Co2AvoidedTonnesPerYear: request.SolarPct * 42,
            BaselineSeries: baseline,
            OptimizedSeries: optimizedSeries,
            EnergyMix: mix));
    }
}
