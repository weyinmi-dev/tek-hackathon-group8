using Microsoft.SemanticKernel;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

internal sealed class SemanticKernelNetworkOptimizationSkill(Kernel kernel) : INetworkOptimizationSkill
{
    private const string Prompt = """
        You are a telecom network capacity engineer. Looking at the JSON array of network events
        below, propose concrete optimization actions for towers that show stress patterns.

        Return ONLY a JSON object of the form:
        { "items": [
            {
              "towerCode":       string (must appear in the events),
              "type":            "loadBalance" | "powerAdjust" | "routeReconfigure" | "antennaRetune" | "capacityExpansion",
              "estimatedImpact": number in [0, 1] — your confidence the action will materially help,
              "rationale":       short string explaining why this action fits the observed pattern
            }
        ] }

        Rules:
          - Only propose actions when there is supporting evidence in the events.
          - At most one action per tower per type.
          - If no actions are warranted, return { "items": [] }.

        {{$raw_context}}
        Events:
        {{$events}}
        """;

    public async Task<Result<IReadOnlyList<ProposedOptimization>>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default)
    {
        var args = new KernelArguments
        {
            ["events"] = eventsJson,
            ["raw_context"] = SemanticKernelNetworkAnomalySkill.BuildRawContextBlock(rawContext),
        };
        Result<List<ProposedOptimization>> result =
            await KernelJsonInvoker.InvokeAsync<List<ProposedOptimization>>(kernel, Prompt, args, cancellationToken);

        return result.IsSuccess
            ? Result.Success<IReadOnlyList<ProposedOptimization>>(result.Value)
            : Result.Failure<IReadOnlyList<ProposedOptimization>>(result.Error);
    }
}
