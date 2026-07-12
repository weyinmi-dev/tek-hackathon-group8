using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Agents.Agents;
using Modules.Ai.Application.Incidents;
using Modules.Alerts.Api;

namespace Modules.Ai.Agents.Workflows.IncidentInvestigation;

/// <summary>
/// Builds IncidentInvestigationWorkflow — the directive's headline event flow (Phase 2 §7.3):
///
///   AlarmReceived ─▶ correlate ─▶ root-cause ─▶ runbook-policy ─▶ notify
///
/// A straight chain, no branches: every alarm is worth the same four steps. Three of them are
/// deterministic and one is a single model call, which is the whole point — the reasoning is isolated
/// to the step that needs reasoning, and the recommendation an engineer acts on is reproducible.
///
/// Each arrow is a superstep boundary, so the host's checkpoint manager persists the message in
/// flight; a crash after the model call resumes with the finding in hand and does not pay for it twice.
/// </summary>
public sealed class IncidentInvestigationWorkflowBuilder(
    IAlertsApi alerts,
    RootCauseAgentBuilder rootCauseAgentBuilder,
    IIncidentNotifier notifier)
{
    public Workflow Build()
    {
        var correlate = new CorrelationExecutor(alerts);
        var rootCause = new RootCauseExecutor(rootCauseAgentBuilder.Build());
        var runbook = new RunbookPolicyExecutor();
        var notify = new NotificationExecutor(notifier);

        return new WorkflowBuilder(correlate)
            .AddEdge(correlate, rootCause)
            .AddEdge(rootCause, runbook)
            .AddEdge(runbook, notify)
            .WithOutputFrom(notify)
            .Build();
    }
}
