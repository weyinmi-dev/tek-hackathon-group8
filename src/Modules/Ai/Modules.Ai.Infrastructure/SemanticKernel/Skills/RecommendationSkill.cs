using System.ComponentModel;
using Microsoft.SemanticKernel;
using Modules.Network.Domain.Runbooks;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill — surfaces the Network module's NOC runbooks to the LLM as concrete next steps.
/// The recommendation logic is business policy and lives in <see cref="RunbookPolicy"/>
/// (Network domain); this skill is only the thin tool wrapper the model calls.
/// </summary>
public sealed class RecommendationSkill
{
    [KernelFunction("suggest_actions")]
    [Description("Given a root-cause classification (e.g. 'fiber_cut', 'power_failure', 'congestion', 'thermal'), return a numbered list of 3 concrete actions an on-call engineer should take.")]
    public string SuggestActions(
        [Description("Root cause class")] string rootCause,
        [Description("Affected tower code")] string towerCode = "")
        => RunbookPolicy.Recommend(rootCause, towerCode);
}
