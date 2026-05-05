using FluentValidation;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Ai.Infrastructure.Pipeline.Validators;

internal sealed class ProposedOptimizationValidator : AbstractValidator<ProposedOptimization>
{
    public ProposedOptimizationValidator()
    {
        RuleFor(o => o.TowerCode)
            .NotEmpty()
            .WithMessage("Optimization is missing required field 'towerCode'.");

        RuleFor(o => o.Type)
            .IsInEnum()
            .WithMessage("Optimization 'type' is not in the allowed OptimizationType range.");

        RuleFor(o => o.EstimatedImpact)
            .InclusiveBetween(0m, 1m)
            .WithMessage("Optimization 'estimatedImpact' must be in [0, 1].");

        RuleFor(o => o.Rationale)
            .NotEmpty()
            .WithMessage("Optimization 'rationale' is required so operators can audit suggestions.");
    }
}
