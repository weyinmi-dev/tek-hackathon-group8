using Modules.Ai.Application.Incidents;

namespace Modules.Ai.Agents.Workflows.IncidentInvestigation;

// The messages that flow along IncidentInvestigationWorkflow's edges (Phase 2 §7.3):
//
//   AlarmReceived → correlate → root-cause → runbook-policy → notify
//
// Each message carries the whole accumulated story forward rather than leaving state in executor
// fields. That is what makes the executors stateless and the checkpointed resume correct: a run that
// dies after the (expensive, non-deterministic) root-cause step resumes holding the finding, and
// never asks the model twice.

/// <summary>
/// Workflow input. Built by the host from the <c>AlarmReceived</c> integration event, so the
/// executors need only the alarm — never a repository — to do their work.
/// </summary>
public sealed record InvestigateAlarmRequest(
    Guid AlertId,
    string Code,
    string Severity,
    string TowerCode,
    string Region,
    string Title,
    string AiCause,
    double Confidence,
    DateTime DetectedAtUtc);

/// <summary>
/// The alarm placed in context: what else is currently firing around it. <paramref name="IsRegionWide"/>
/// is the signal that separates "this tower has a problem" from "this region has a problem", which is
/// the single most important thing to tell an operator and the thing a per-alert view cannot show.
/// </summary>
public sealed record CorrelatedIncident(
    InvestigateAlarmRequest Alarm,
    IReadOnlyList<string> RelatedAlertCodes,
    bool IsRegionWide);

/// <summary>The one non-deterministic step's output: the agent's hypothesis and what it leant on.</summary>
public sealed record RootCauseFinding(CorrelatedIncident Incident, string Cause);

/// <summary>The deterministic recommendation derived from the finding.</summary>
public sealed record RecommendedRunbook(RootCauseFinding Finding, IReadOnlyList<RunbookAction> Actions);

/// <summary>Terminal output — surfaced by the workflow so the host can log the outcome.</summary>
public sealed record InvestigationCompleted(
    Guid AlertId,
    string Cause,
    int ActionCount,
    int CorrelatedAlerts);
