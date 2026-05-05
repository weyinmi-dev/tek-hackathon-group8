using Microsoft.SemanticKernel;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

internal sealed class SemanticKernelNetworkTopologyMappingSkill(Kernel kernel) : INetworkTopologyMappingSkill
{
    private const string Prompt = """
        You map network event batches to topology updates: which towers changed status, and
        what their latest reported metrics are.

        Return ONLY a JSON object of the form:
        {
          "statusChanges": [
            { "towerCode": string, "previousStatus": string, "newStatus": string, "reason": string|null }
          ],
          "metricUpdates": [
            { "towerCode": string, "signalPct": number|null, "loadPct": number|null, "latencyMs": number|null }
          ]
        }

        Rules:
          - Only emit a status change when the tower's status materially differs from its earliest
            observation in this batch.
          - Only emit a metric update when the tower's latest metrics noticeably differ from its
            earliest in this batch.
          - If neither holds, return { "statusChanges": [], "metricUpdates": [] }.
          - Use the towerCode strings as they appear in the events.

        Events:
        {{$events}}
        """;

    public async Task<Result<TopologyDelta?>> InvokeAsync(
        string eventsJson,
        CancellationToken cancellationToken = default)
    {
        var args = new KernelArguments { ["events"] = eventsJson };

        // Topology returns a single object (not an items envelope), so use the raw invoker
        // and treat an "everything-empty" object as a deliberate null delta.
        Result<TopologyDelta> result =
            await KernelJsonInvoker.InvokeAsync<TopologyDelta>(kernel, Prompt, args, cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TopologyDelta?>(result.Error);
        }

        TopologyDelta delta = result.Value;
        bool empty = (delta.StatusChanges?.Count ?? 0) == 0 &&
                     (delta.MetricUpdates?.Count ?? 0) == 0;

        return Result.Success<TopologyDelta?>(empty ? null : delta);
    }
}
