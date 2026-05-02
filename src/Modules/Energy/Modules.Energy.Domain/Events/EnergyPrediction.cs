using SharedKernel;

namespace Modules.Energy.Domain.Events;

public enum PredictionKind
{
    GeneratorFault = 0,
    BatteryEol = 1,
    GridOutage = 2,
}

/// <summary>
/// Forward-looking prediction tied to a site (e.g. "87% gen fault probability by 18:42").
/// Populated by the ticker on a slower cadence than the live telemetry; consumed by the
/// Anomalies / Optimization screens and by RecommendationSkill.
/// </summary>
public sealed class EnergyPrediction : Entity
{
    private EnergyPrediction(
        Guid id, string siteCode, PredictionKind kind, double probability,
        DateTime predictedAtUtc, DateTime windowEndsUtc, string detail) : base(id)
    {
        SiteCode = siteCode;
        Kind = kind;
        Probability = probability;
        PredictedAtUtc = predictedAtUtc;
        WindowEndsUtc = windowEndsUtc;
        Detail = detail;
    }

    private EnergyPrediction() { }

    public string SiteCode { get; private set; } = null!;
    public PredictionKind Kind { get; private set; }
    public double Probability { get; private set; }
    public DateTime PredictedAtUtc { get; private set; }
    public DateTime WindowEndsUtc { get; private set; }
    public string Detail { get; private set; } = null!;

    public static EnergyPrediction Create(string siteCode, PredictionKind kind, double probability,
        DateTime windowEndsUtc, string detail) =>
        new(Guid.NewGuid(), siteCode, kind, probability, DateTime.UtcNow, windowEndsUtc, detail);
}

public interface IEnergyPredictionRepository
{
    Task AddAsync(EnergyPrediction p, CancellationToken ct = default);
    Task<IReadOnlyList<EnergyPrediction>> ListActiveAsync(CancellationToken ct = default);
}
