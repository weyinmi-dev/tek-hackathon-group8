using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Network.Domain.Optimizations;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

internal sealed class CreateOptimizationCommandHandler(
    IOptimizationRepository repository,
    ILogger<CreateOptimizationCommandHandler> logger)
    : ICommandHandler<CreateOptimizationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateOptimizationCommand request, CancellationToken cancellationToken)
    {
        Optimization optimization;
        try
        {
            optimization = Optimization.Propose(
                ingestionRunId: request.IngestionRunId,
                towerCode: request.TowerCode,
                anomalyFingerprint: request.AnomalyFingerprint,
                type: request.Type,
                estimatedImpact: request.EstimatedImpact,
                rationale: request.Rationale,
                proposedAt: request.ProposedAt);
        }
        catch (ArgumentException ex)
        {
            // Defensive — the decision engine + AI validator already filter these,
            // but a malformed action shouldn't crash the whole stage.
            return Result.Failure<Guid>(Error.Problem(
                "Network.Ingestion.InvalidOptimization", ex.Message));
        }

        await repository.AddAsync(optimization, cancellationToken);
        // No SaveChanges here — the orchestrating ApplyPipelineActionsCommandHandler
        // commits the unit of work after dispatching every per-action sub-command.

        logger.LogInformation(
            "Optimization {OptimizationId} proposed for tower {TowerCode} (type {Type}, impact {Impact})",
            optimization.Id, optimization.TowerCode, optimization.Type, optimization.EstimatedImpact);

        return Result.Success(optimization.Id);
    }
}
