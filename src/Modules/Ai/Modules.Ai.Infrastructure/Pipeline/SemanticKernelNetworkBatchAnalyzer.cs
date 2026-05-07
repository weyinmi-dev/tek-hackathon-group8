using Application.Abstractions.Storage;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Modules.Ai.Infrastructure.Pipeline.Skills;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline;

/// <summary>
/// Composes the four Stage-2 skills, schema-validates each output, and retries the
/// individual call once on validation failure (the model occasionally produces a
/// shape-shifted response on the first try). Anything that fails twice surfaces as
/// a typed pipeline error — the orchestrator is then free to mark the run failed.
///
/// When <c>mcpFilePath</c> is provided (a telcopilot-relative path set by the
/// orchestrator after staging the upload via <c>IFileStagingService</c>), the raw
/// file content is read and forwarded to each skill as additional prompt context so
/// the model can ground analysis in the original document rather than only the
/// structured events array.
/// </summary>
internal sealed class SemanticKernelNetworkBatchAnalyzer(
    INetworkAnomalySkill anomalySkill,
    INetworkOptimizationSkill optimizationSkill,
    INetworkTopologyMappingSkill topologySkill,
    INetworkEnergySkill energySkill,
    IFileStagingService staging,
    IValidator<AiAnalysisResult> resultValidator,
    ILogger<SemanticKernelNetworkBatchAnalyzer> logger) : INetworkBatchAnalyzer
{
    private const int MaxAttempts = 2;

    public async Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEvent> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Result.Success(AiAnalysisResult.Empty);
        }

        string eventsJson = AiPipelineJson.SerializeEvents(events);

        // Read raw file content from the MCP-accessible telcopilot directory so the
        // model can correlate structured events with the original source material.
        // Failure is silently swallowed — skills fall back to events-only context.
        string? rawContext = mcpFilePath is not null
            ? await staging.TryReadTextAsync(mcpFilePath, cancellationToken)
            : null;

        if (rawContext is not null)
        {
            logger.LogInformation(
                "Run {IngestionRunId}: enriching skills with {Chars} chars of raw file context from {McpFilePath}",
                ingestionRunId, rawContext.Length, mcpFilePath);
        }

        Result<IReadOnlyList<DetectedAnomaly>> anomalies =
            await InvokeWithRetry(
                attempt => anomalySkill.InvokeAsync(eventsJson, rawContext, cancellationToken),
                "anomaly", ingestionRunId);
        if (anomalies.IsFailure) return Result.Failure<AiAnalysisResult>(anomalies.Error);

        Result<IReadOnlyList<ProposedOptimization>> optimizations =
            await InvokeWithRetry(
                attempt => optimizationSkill.InvokeAsync(eventsJson, rawContext, cancellationToken),
                "optimization", ingestionRunId);
        if (optimizations.IsFailure) return Result.Failure<AiAnalysisResult>(optimizations.Error);

        Result<TopologyDelta?> topology =
            await InvokeWithRetry(
                attempt => topologySkill.InvokeAsync(eventsJson, rawContext, cancellationToken),
                "topology", ingestionRunId);
        if (topology.IsFailure) return Result.Failure<AiAnalysisResult>(topology.Error);

        Result<string> energy =
            await InvokeWithRetry(
                attempt => energySkill.InvokeAsync(eventsJson, rawContext, cancellationToken),
                "energy", ingestionRunId);
        if (energy.IsFailure) return Result.Failure<AiAnalysisResult>(energy.Error);

        var combined = new AiAnalysisResult(anomalies.Value, optimizations.Value, topology.Value, energy.Value);

        ValidationResult validation = await resultValidator.ValidateAsync(combined, cancellationToken);
        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Combined AI result failed schema validation for run {IngestionRunId}: {Errors}",
                ingestionRunId,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

            return Result.Failure<AiAnalysisResult>(Error.Failure(
                "Network.Ingestion.AiSchemaInvalid",
                $"AI output failed schema validation: {validation.Errors[0].ErrorMessage}"));
        }

        logger.LogInformation(
            "Run {IngestionRunId}: AI produced {AnomalyCount} anomalies, {OptimizationCount} optimizations, topology={TopologyPresent}, energyObs={EnergyPresent}",
            ingestionRunId, anomalies.Value.Count, optimizations.Value.Count, topology.Value is not null, !string.IsNullOrWhiteSpace(energy.Value));

        return Result.Success(combined);
    }

    private async Task<Result<T>> InvokeWithRetry<T>(
        Func<int, Task<Result<T>>> invoker,
        string skillName,
        Guid ingestionRunId)
    {
        Result<T> last = default!;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            last = await invoker(attempt);

            if (last.IsSuccess)
            {
                return last;
            }

            // Retry only the kinds of failures the model might recover from on a fresh call.
            // Hard infrastructure failures (auth, throttling, cancellation) bubble up unchanged.
            if (!IsRetryableError(last.Error))
            {
                return last;
            }

            logger.LogWarning(
                "Run {IngestionRunId} skill {SkillName} attempt {Attempt} failed ({Code}): {Description} — retrying",
                ingestionRunId, skillName, attempt, last.Error.Code, last.Error.Description);
        }

        return last;
    }

    private static bool IsRetryableError(Error error) =>
        error.Code is "Network.Ingestion.AiMalformedJson"
                   or "Network.Ingestion.AiNullPayload"
                   or "Network.Ingestion.AiEmptyResponse"
                   or "Network.Ingestion.AiSchemaInvalid";
}
