using FluentValidation;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Ai.Infrastructure.Pipeline.Validators;

/// <summary>
/// Schema enforcement for one anomaly returned by the AI. Anything that fails here
/// is rejected by the analyzer wrapper before the decision layer ever sees it.
/// Confidence is in [0, 1]; tower codes must be non-blank; the enum range is implicit
/// (System.Text.Json fails at deserialization for unknown enum values).
/// </summary>
internal sealed class DetectedAnomalyValidator : AbstractValidator<DetectedAnomaly>
{
    public DetectedAnomalyValidator()
    {
        RuleFor(a => a.TowerCode)
            .NotEmpty()
            .WithMessage("Anomaly is missing required field 'towerCode'.");

        RuleFor(a => a.Type)
            .IsInEnum()
            .WithMessage("Anomaly 'type' is not in the allowed AnomalyType range.");

        RuleFor(a => a.Severity)
            .IsInEnum()
            .WithMessage("Anomaly 'severity' is not in the allowed PipelineAlertSeverity range.");

        RuleFor(a => a.Confidence)
            .InclusiveBetween(0m, 1m)
            .WithMessage("Anomaly 'confidence' must be in [0, 1].");

        RuleFor(a => a.DetectedAt)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("Anomaly 'detectedAt' is missing or unparseable.");

        RuleFor(a => a.Explanation)
            .NotEmpty()
            .WithMessage("Anomaly 'explanation' must be a non-empty string for traceability.");

        RuleFor(a => a.Metrics)
            .NotNull()
            .WithMessage("Anomaly 'metrics' must be present (use {} when no metrics apply).");
    }
}
