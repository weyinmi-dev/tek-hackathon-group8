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

        // ── Cost model ──────────────────────────────────────────────────────────
        // Both curves are exposed to the diesel price, because both burn diesel. The baseline is
        // the fleet as it runs today — heavily diesel-dependent, so its OPEX moves almost fully
        // with the pump price. The optimized fleet burns less diesel, so it is exposed to only the
        // share it still burns.
        //
        // That asymmetry is the whole point of the projection: a *rising* diesel price makes
        // optimization worth MORE, not less. The earlier model held the baseline flat and let the
        // price only push the optimized line up, which inverted the economics — at a high enough
        // price it reported that optimizing cost you money. Savings are now non-negative by
        // construction (see `daily` below), because they are the diesel the optimized fleet did not
        // buy plus the solar and battery gains.
        const double ReferenceOpexMPerDay = 21.0;   // fleet OPEX at the reference pump price
        const double ReferencePriceNgnPerLitre = 900.0;
        const double DieselSensitivity = 0.004;     // ₦M/day of fleet OPEX per ₦1/L, at today's diesel share

        double priceDelta = request.DieselPriceNgnPerLitre - ReferencePriceNgnPerLitre;

        // Baseline: fully exposed to the price move.
        double baselineOpex = Math.Max(1.0, ReferenceOpexMPerDay + priceDelta * DieselSensitivity);

        // How much diesel the optimization displaces. Capped: no configuration of these two sliders
        // takes a tower fully off diesel, and claiming otherwise would overstate the saving.
        int dieselReduction = (int)Math.Round(request.SolarPct * 0.5 + (request.BatteryThresholdPct - 50) * 0.3);
        dieselReduction = Math.Clamp(dieselReduction, 0, 90);
        double retainedDieselShare = 1.0 - dieselReduction / 100.0;

        double solarSavings = request.SolarPct * 0.12;
        double battSavings = (request.BatteryThresholdPct - 50) * 0.04;

        // Optimized: exposed to the price move only on the diesel it still burns, then reduced by
        // the solar and battery gains.
        double optimized = Math.Max(
            6.0,
            ReferenceOpexMPerDay + priceDelta * DieselSensitivity * retainedDieselShare
                - solarSavings - battSavings);

        double daily = Math.Max(0, baselineOpex - optimized);
        double annual = daily * 365 / 1000.0;

        double[] baseline = new double[30];
        double[] optimizedSeries = new double[30];
        for (int i = 0; i < 30; i++)
        {
            baseline[i] = baselineOpex + (i % 5 - 2) * 0.4;
            optimizedSeries[i] = Math.Max(6.0, optimized + Math.Sin(i / 3.0) * 0.8);
        }

        IReadOnlyList<EnergyMixSlice> mix =
        [
            new EnergyMixSlice("Diesel", 100 * onGen / total),
            new EnergyMixSlice("Grid",   100 * onGrid / total),
            new EnergyMixSlice("Battery",100 * onBatt / total),
            new EnergyMixSlice("Solar",  100 * onSolar / total),
        ];

        return Result.Success(new OptimizationProjectionResponse(
            BaselineDailyOpexMillionsNgn: baselineOpex,
            OptimizedDailyOpexMillionsNgn: optimized,
            DailySavingsMillionsNgn: daily,
            AnnualSavingsBillionsNgn: annual,
            DieselReductionPct: dieselReduction,
            Co2AvoidedTonnesPerYear: request.SolarPct * 42,
            BaselineSeries: baseline,
            OptimizedSeries: optimizedSeries,
            EnergyMix: mix));
    }
}
