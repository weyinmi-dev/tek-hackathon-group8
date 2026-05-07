using FluentValidation;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Ai.Infrastructure.Pipeline.Validators;

internal sealed class TopologyDeltaValidator : AbstractValidator<TopologyDelta>
{
    public TopologyDeltaValidator()
    {
        RuleFor(t => t.StatusChanges)
            .NotNull()
            .WithMessage("TopologyDelta 'statusChanges' must be present (use [] when none).");

        RuleForEach(t => t.StatusChanges).ChildRules(c =>
        {
            c.RuleFor(sc => sc.TowerCode).NotEmpty()
                .WithMessage("StatusChange 'towerCode' must be non-empty.");
            c.RuleFor(sc => sc.NewStatus).NotEmpty()
                .WithMessage("StatusChange 'newStatus' must be non-empty.");
        });

        RuleFor(t => t.MetricUpdates)
            .NotNull()
            .WithMessage("TopologyDelta 'metricUpdates' must be present (use [] when none).");

        RuleForEach(t => t.MetricUpdates).ChildRules(c =>
        {
            c.RuleFor(m => m.TowerCode).NotEmpty()
                .WithMessage("MetricUpdate 'towerCode' must be non-empty.");
            c.RuleFor(m => m.SignalPct)
                .Must(v => v is null or >= 0 and <= 100)
                .WithMessage("MetricUpdate 'signalPct' must be in [0, 100] when present.");
            c.RuleFor(m => m.LoadPct)
                .Must(v => v is null or >= 0 and <= 100)
                .WithMessage("MetricUpdate 'loadPct' must be in [0, 100] when present.");
            c.RuleFor(m => m.LatencyMs)
                .Must(v => v is null or >= 0)
                .WithMessage("MetricUpdate 'latencyMs' must be non-negative when present.");
        });
    }
}
