using System.Text.Json;
using Microsoft.SemanticKernel;
using Modules.Energy.Api;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

internal sealed class SemanticKernelNetworkEnergySkill(Kernel kernel, IEnergyApi energy) : INetworkEnergySkill
{
    private const string Prompt = """
        You are a telecom NOC analyst with specialisation in energy telemetry. Given the
        JSON array of network events below and the live energy snapshot, identify
        energy-relevant observations (fuel theft signatures, generator overuse,
        rapid battery degradation, sites that are good candidates for solar conversion,
        or 'no_issue'). Use the live energy snapshot to ground any claims.

        Return ONLY a JSON object of the form:
        { "observations": [
           { "siteCode": string, "kind": "dieselTheft" | "generatorOveruse" | "batteryDegrade" | "solarOpportunity" | "no_issue", "confidence": number in [0,1], "explanation": string }
        ] }

        Rules:
        - Use tower/site codes from the events or the energy snapshot; do not invent codes.
        - If no observations, return { "observations": [] }.

        {{$raw_context}}
        Events:
        {{$events}}

        Live energy snapshot (do NOT fabricate values; use provided numbers):
        {{$energy_snapshot}}
        """;

    public async Task<Result<string>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default)
    {
        // Fetch live energy state to ground the model's reasoning.
        var sites = await energy.ListSitesAsync(cancellationToken);
        var anomalies = await energy.ListAnomaliesAsync(50, cancellationToken);

        var snapshot = new { sites, anomalies };
        string snapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = false });

        var args = new KernelArguments
        {
            ["events"] = eventsJson,
            ["energy_snapshot"] = snapshotJson,
            ["raw_context"] = SemanticKernelNetworkAnomalySkill.BuildRawContextBlock(rawContext),
        };
        Result<string> result = await KernelJsonInvoker.InvokeAsync<string>(kernel, Prompt, args, cancellationToken);

        return result.IsSuccess
            ? Result.Success(result.Value)
            : Result.Failure<string>(result.Error);
    }
}
