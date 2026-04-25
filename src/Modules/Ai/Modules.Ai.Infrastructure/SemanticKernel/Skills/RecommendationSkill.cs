using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill — emits operator playbooks the LLM can return as concrete next steps.
/// Pure business logic: maps a root-cause class onto the standard NOC runbook.
/// Real production code would back this with a YAML runbook store; for the
/// hackathon the heuristic is enough to prove the architecture.
/// </summary>
public sealed class RecommendationSkill
{
    [KernelFunction("suggest_actions")]
    [Description("Given a root-cause classification (e.g. 'fiber_cut', 'power_failure', 'congestion', 'thermal'), return a numbered list of 3 concrete actions an on-call engineer should take.")]
    public string SuggestActions(
        [Description("Root cause class")] string rootCause,
        [Description("Affected tower code")] string towerCode = "")
    {
        const StringComparison ic = StringComparison.OrdinalIgnoreCase;
        string normalized = (rootCause ?? "").Trim();
        return normalized switch
        {
            var c when c.Contains("fiber", ic) => $"""
                1. Dispatch field-team-3 to the nearest fiber junction serving {Pretty(towerCode)} (target ETA <30 min)
                2. Auto-shed traffic from {Pretty(towerCode)} → adjacent towers in the same region
                3. Open ticket with civil-works contractor — request immediate halt if works in progress
                """,
            var c when c.Contains("power", ic) || c.Contains("grid", ic) => $"""
                1. Engage genset failover for the affected cluster within 15 min
                2. Notify IKEDC / DisCo of the impacted feeder
                3. Pre-position fuel for next 12h based on fault duration trend
                """,
            var c when c.Contains("congest", ic) || c.Contains("load", ic) => $"""
                1. Trigger automated load-shed onto idle neighbouring cells
                2. Reprioritise voice-over-data scheduling for {Pretty(towerCode)}
                3. Open capacity ticket — add carrier or upgrade backhaul
                """,
            var c when c.Contains("thermal", ic) || c.Contains("predict", ic) => $"""
                1. Schedule preventive maintenance window in next 2h
                2. Pre-provision a spare amplifier for hot-swap
                3. Subscribe NOC to thermal-trend alerts at 80% threshold
                """,
            _ => $"""
                1. Open a P2 ticket and assign Tier-2 NOC on-call
                2. Capture a 5-minute deep telemetry snapshot for the impacted segment
                3. If subscriber impact >5k, notify customer-care and post status page
                """,
        };
    }

    private static string Pretty(string s) => string.IsNullOrWhiteSpace(s) ? "the affected tower" : s;
}
