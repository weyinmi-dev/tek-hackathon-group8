using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Application.Incidents;
using Modules.Alerts.Api;

namespace Modules.Ai.Agents.Workflows.IncidentInvestigation;

// The four executors of IncidentInvestigationWorkflow. Exactly one of them calls a model — the middle
// one, where the reasoning actually is (Phase 2 §7.3). Correlation and the runbook are deterministic,
// which means the same alarm plus the same live alerts always produces the same context and the same
// recommended actions; only the prose cause can vary. Alerts are read through Modules.Alerts.Api, so
// the agent layer touches no repository.

/// <summary>
/// Step 1 — place the alarm in context. Deterministic: pulls the currently-active alerts and asks two
/// questions of them. Is this tower already in trouble, and is the whole region?
/// </summary>
public sealed partial class CorrelationExecutor(IAlertsApi alerts) : Executor("correlate")
{
    /// <summary>Three or more active alerts across a region is the point at which this stops being a tower fault.</summary>
    private const int RegionWideThreshold = 3;

    [MessageHandler]
    public async ValueTask<CorrelatedIncident> HandleAsync(InvestigateAlarmRequest alarm, IWorkflowContext context)
    {
        IReadOnlyList<AlertSnapshot> active = await alerts.ListActiveAsync();

        // Everything else currently firing on the same tower. The alarm itself is excluded by code —
        // it is already in the list by the time the event reaches us (it was committed before publish).
        List<string> related = active
            .Where(a => !string.Equals(a.Code, alarm.Code, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.TowerCode, alarm.TowerCode, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Code)
            .ToList();

        int regionAlerts = active.Count(a =>
            string.Equals(a.Region, alarm.Region, StringComparison.OrdinalIgnoreCase));

        return new CorrelatedIncident(alarm, related, regionAlerts >= RegionWideThreshold);
    }
}

/// <summary>
/// Step 2 — the single model call. The agent gets the alarm, its correlation context, and its tools
/// (tower metrics, prior incidents from the knowledge base); it returns a cause.
/// </summary>
public sealed partial class RootCauseExecutor(AIAgent rootCauseAgent) : Executor("root-cause")
{
    [MessageHandler]
    public async ValueTask<RootCauseFinding> HandleAsync(CorrelatedIncident incident, IWorkflowContext context)
    {
        InvestigateAlarmRequest alarm = incident.Alarm;

        string related = incident.RelatedAlertCodes.Count > 0
            ? string.Join(", ", incident.RelatedAlertCodes)
            : "none";
        string scope = incident.IsRegionWide
            ? "Multiple towers in this region are alerting — consider regional causes (power, backhaul, weather)."
            : "No wider regional pattern — this looks tower-local.";

        string prompt =
            $"""
             Alarm {alarm.Code} ({alarm.Severity}) on tower {alarm.TowerCode} in {alarm.Region}.
             Title: {alarm.Title}
             Detected at: {alarm.DetectedAtUtc:u}
             Cause suspected at detection time: {alarm.AiCause} (confidence {alarm.Confidence:P0})
             Other active alerts on this tower: {related}
             {scope}

             Determine the most likely root cause. Ground it in the tower's current metrics and in
             prior incidents. Answer with the cause, briefly.
             """;

        AgentResponse response = await rootCauseAgent.RunAsync(prompt);

        // The detection-time cause is the fallback, not a fabrication: if the model returns nothing we
        // would rather carry the hypothesis we already had than invent a conclusion.
        string cause = response.ToString() is { Length: > 0 } text
            ? text.Trim()
            : alarm.AiCause;

        return new RootCauseFinding(incident, cause);
    }
}

/// <summary>
/// Step 3 — the runbook. Deterministic on purpose: the actions an engineer is told to take must be
/// reproducible and reviewable, and must not vary with a model's mood. The agent says what broke;
/// policy says what to do about it.
/// </summary>
public sealed partial class RunbookPolicyExecutor() : Executor("runbook-policy")
{
    [MessageHandler]
    public ValueTask<RecommendedRunbook> HandleAsync(RootCauseFinding finding, IWorkflowContext context)
    {
        InvestigateAlarmRequest alarm = finding.Incident.Alarm;
        bool critical = string.Equals(alarm.Severity, "Critical", StringComparison.OrdinalIgnoreCase);

        RunbookAction[] actions =
        [
            new(1,
                critical ? $"Dispatch a field engineer to {alarm.TowerCode}."
                         : $"Open a monitoring ticket for {alarm.TowerCode}.",
                critical ? "Critical severity — a truck roll is justified before further diagnosis."
                         : "Below critical — track it, but do not spend a field visit yet."),

            new(2,
                finding.Incident.IsRegionWide
                    ? $"Check regional power and backhaul for {alarm.Region} before touching the tower."
                    : $"Verify {alarm.TowerCode}'s power, backhaul and antenna alignment on site.",
                finding.Incident.IsRegionWide
                    ? "Several towers in this region are alerting — a shared upstream fault is the cheaper hypothesis to rule out first."
                    : "No regional pattern, so the fault is most likely local to the tower."),

            new(3,
                finding.Incident.RelatedAlertCodes.Count > 0
                    ? $"Review the related alerts on this tower ({string.Join(", ", finding.Incident.RelatedAlertCodes)}) and close them together."
                    : "Re-check the tower after remediation and confirm the alert clears.",
                finding.Incident.RelatedAlertCodes.Count > 0
                    ? "The same underlying fault is raising several alarms; resolving it once should clear them all."
                    : "Confirms the fix rather than assuming it."),
        ];

        return ValueTask.FromResult(new RecommendedRunbook(finding, actions));
    }
}

/// <summary>
/// Step 4 — hand the recommendation to whoever is listening. Where it lands is the notifier's problem,
/// not the workflow's.
/// </summary>
public sealed partial class NotificationExecutor(IIncidentNotifier notifier) : Executor("notify")
{
    [MessageHandler]
    public async ValueTask<InvestigationCompleted> HandleAsync(RecommendedRunbook runbook, IWorkflowContext context)
    {
        CorrelatedIncident incident = runbook.Finding.Incident;
        InvestigateAlarmRequest alarm = incident.Alarm;

        await notifier.NotifyAsync(new IncidentNotification(
            AlertId: alarm.AlertId,
            AlertCode: alarm.Code,
            Severity: alarm.Severity,
            TowerCode: alarm.TowerCode,
            Region: alarm.Region,
            RootCause: runbook.Finding.Cause,
            Actions: runbook.Actions,
            CorrelatedAlertCodes: incident.RelatedAlertCodes));

        return new InvestigationCompleted(
            alarm.AlertId,
            runbook.Finding.Cause,
            runbook.Actions.Count,
            incident.RelatedAlertCodes.Count);
    }
}
