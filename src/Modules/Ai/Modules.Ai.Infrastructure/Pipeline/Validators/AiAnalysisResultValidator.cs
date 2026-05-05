using FluentValidation;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Ai.Infrastructure.Pipeline.Validators;

/// <summary>
/// Top-level validator: composes the per-shape validators and adds cross-cutting
/// invariants (e.g. anomaly + optimization arrays must be present).
/// </summary>
internal sealed class AiAnalysisResultValidator : AbstractValidator<AiAnalysisResult>
{
    public AiAnalysisResultValidator()
    {
        RuleFor(r => r.Anomalies)
            .NotNull()
            .WithMessage("AiAnalysisResult 'anomalies' array must be present (use [] when none).");

        RuleFor(r => r.Optimizations)
            .NotNull()
            .WithMessage("AiAnalysisResult 'optimizations' array must be present (use [] when none).");

        RuleForEach(r => r.Anomalies).SetValidator(new DetectedAnomalyValidator());
        RuleForEach(r => r.Optimizations).SetValidator(new ProposedOptimizationValidator());

        When(r => r.Topology is not null, () =>
            RuleFor(r => r.Topology!).SetValidator(new TopologyDeltaValidator()));
    }
}
