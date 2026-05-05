using Microsoft.SemanticKernel;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

internal sealed class SemanticKernelNetworkAnomalySkill(Kernel kernel) : INetworkAnomalySkill
{
    private const string Prompt = """
        You are a telecom NOC analyst. Look at the JSON array of network events below and detect
        anomalies. An anomaly is a tower exhibiting a degraded pattern relative to its baseline,
        not just a single metric outside a normal band.

        Return ONLY a JSON object of the form:
        { "items": [
            {
              "towerCode":   string,
              "type":        "signalDrop" | "loadSpike" | "outagePattern" | "latencyAnomaly" | "packetLoss" | "powerInstability",
              "severity":    "info" | "warn" | "critical",
              "confidence":  number in [0, 1],
              "detectedAt":  ISO-8601 timestamp,
              "explanation": string explaining the evidence in one or two sentences,
              "metrics":     object mapping metric name to numeric value
            }
        ] }

        Rules:
          - Use the same towerCode that appears in the events; do not invent codes.
          - One item per (tower, anomaly type) — do not duplicate.
          - If no anomalies are detected, return { "items": [] }.
          - confidence must reflect how strongly the evidence supports the anomaly.

        Events:
        {{$events}}
        """;

    public async Task<Result<IReadOnlyList<DetectedAnomaly>>> InvokeAsync(
        string eventsJson,
        CancellationToken cancellationToken = default)
    {
        var args = new KernelArguments { ["events"] = eventsJson };
        Result<List<DetectedAnomaly>> result =
            await KernelJsonInvoker.InvokeAsync<List<DetectedAnomaly>>(kernel, Prompt, args, cancellationToken);

        return result.IsSuccess
            ? Result.Success<IReadOnlyList<DetectedAnomaly>>(result.Value)
            : Result.Failure<IReadOnlyList<DetectedAnomaly>>(result.Error);
    }
}
