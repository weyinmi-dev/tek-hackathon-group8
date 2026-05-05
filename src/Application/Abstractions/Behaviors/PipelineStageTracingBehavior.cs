using System.Diagnostics;
using Application.Abstractions.Pipeline;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

/// <summary>
/// Logs every stage transition of the network-ops ingestion pipeline
/// (Ingestion → AI → Decision → Persistence → Projection) with the
/// owning IngestionRunId pushed into the Serilog log context, so every
/// log line emitted by the inner handler is correlated.
/// </summary>
internal sealed class PipelineStageTracingBehavior<TRequest, TResponse>(
    ILogger<PipelineStageTracingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IIngestionPipelineRequest
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Guid runId = request.IngestionRunId;
        string stage = request.StageName;

        using IDisposable runScope = LogContext.PushProperty("IngestionRunId", runId);
        using IDisposable stageScope = LogContext.PushProperty("PipelineStage", stage);

        long startTimestamp = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "Pipeline stage {PipelineStage} starting for run {IngestionRunId}",
            stage, runId);

        TResponse result = await next(cancellationToken);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Pipeline stage {PipelineStage} completed for run {IngestionRunId} in {ElapsedMs}ms",
                stage, runId, elapsed.TotalMilliseconds);
        }
        else
        {
            using (LogContext.PushProperty("Error", result.Error, true))
            {
                logger.LogError(
                    "Pipeline stage {PipelineStage} failed for run {IngestionRunId} after {ElapsedMs}ms",
                    stage, runId, elapsed.TotalMilliseconds);
            }
        }

        return result;
    }
}
