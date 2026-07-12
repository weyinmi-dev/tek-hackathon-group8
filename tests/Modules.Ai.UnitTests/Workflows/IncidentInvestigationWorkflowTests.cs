using FluentAssertions;
using MediatR;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Agents;
using Modules.Ai.Agents.Infrastructure;
using Modules.Ai.Agents.Tools;
using Modules.Ai.Agents.Workflows.IncidentInvestigation;
using Modules.Ai.Application.Incidents;
using Modules.Alerts.Api;
using Xunit;

namespace Modules.Ai.UnitTests.Workflows;

/// <summary>
/// The M12b exit criteria, executed: an alarm goes in, the workflow runs end to end, the runbook comes
/// back with three actions, and a notification is recorded.
///
/// It runs the real graph — correlate → root-cause → runbook-policy → notify — with the deterministic
/// chat client standing in for the model, which is exactly how the app behaves with no Azure key set.
/// The parity harness cannot cover this (it never registers the host), so without these tests the
/// workflow would be wired but unproven.
/// </summary>
public sealed class IncidentInvestigationWorkflowTests
{
    private static readonly Guid AlertId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static InvestigateAlarmRequest Alarm(
        string code = "AL-LOS014-SIGNAL",
        string severity = "Critical",
        string tower = "LOS-T-014",
        string region = "Lagos West") =>
        new(AlertId, code, severity, tower, region,
            Title: "Signal collapse on LOS-T-014",
            AiCause: "Suspected antenna misalignment",
            Confidence: 0.82,
            DetectedAtUtc: new DateTime(2026, 5, 5, 8, 10, 0, DateTimeKind.Utc));

    private static IncidentInvestigationWorkflowBuilder Builder(
        IAlertsApi alerts, IIncidentNotifier notifier)
    {
        // The tools need an ISender, but the deterministic client never issues a tool call, so the
        // fake sender is never reached. Building the real agent keeps the graph identical to production.
        var sender = new UnusedSender();
        var agentBuilder = new RootCauseAgentBuilder(
            new DeterministicChatClient(),
            new NetworkTools(sender),
            new KnowledgeTools(sender));

        return new IncidentInvestigationWorkflowBuilder(alerts, agentBuilder, notifier);
    }

    private static async Task<InvestigationCompleted?> RunAsync(
        IncidentInvestigationWorkflowBuilder builder, InvestigateAlarmRequest alarm)
    {
        Workflow workflow = builder.Build();
        StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, alarm);

        InvestigationCompleted? outcome = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent { Data: InvestigationCompleted completed })
            {
                outcome = completed;
            }
        }

        return outcome;
    }

    [Fact]
    public async Task Workflow_RunsEndToEnd_AndRecordsANotification()
    {
        var notifier = new CapturingNotifier();
        var alerts = new FakeAlertsApi([]);

        InvestigationCompleted? outcome = await RunAsync(Builder(alerts, notifier), Alarm());

        outcome.Should().NotBeNull();
        outcome!.AlertId.Should().Be(AlertId);
        outcome.Cause.Should().NotBeNullOrWhiteSpace();

        IncidentNotification notification = notifier.Sent.Should().ContainSingle().Subject;
        notification.AlertCode.Should().Be("AL-LOS014-SIGNAL");
        notification.TowerCode.Should().Be("LOS-T-014");
        notification.RootCause.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunbookPolicy_AlwaysReturnsThreeActions()
    {
        var notifier = new CapturingNotifier();

        InvestigationCompleted? outcome = await RunAsync(Builder(new FakeAlertsApi([]), notifier), Alarm());

        outcome!.ActionCount.Should().Be(3);
        notifier.Sent[0].Actions.Should().HaveCount(3);
        notifier.Sent[0].Actions.Select(a => a.Order).Should().Equal(1, 2, 3);
        notifier.Sent[0].Actions.Should().OnlyContain(a =>
            !string.IsNullOrWhiteSpace(a.Action) && !string.IsNullOrWhiteSpace(a.Rationale));
    }

    [Fact]
    public async Task Correlation_FindsOtherActiveAlertsOnTheSameTower_AndExcludesTheAlarmItself()
    {
        var notifier = new CapturingNotifier();
        var alerts = new FakeAlertsApi([
            Snapshot("AL-LOS014-SIGNAL", "LOS-T-014", "Lagos West"),   // the alarm itself
            Snapshot("AL-LOS014-LOAD", "LOS-T-014", "Lagos West"),     // same tower — correlated
            Snapshot("AL-ABV007-LOAD", "ABV-T-007", "Abuja"),          // elsewhere — not correlated
        ]);

        InvestigationCompleted? outcome = await RunAsync(Builder(alerts, notifier), Alarm());

        outcome!.CorrelatedAlerts.Should().Be(1);
        notifier.Sent[0].CorrelatedAlertCodes.Should().Equal("AL-LOS014-LOAD");
    }

    [Fact]
    public async Task Correlation_ThreeAlertsInTheRegion_SwitchesTheRunbookToARegionalCause()
    {
        // The tower-local runbook says "go inspect the tower"; the regional one says "rule out shared
        // power/backhaul first". Sending an engineer up a mast during a regional power cut is the
        // failure this branch exists to prevent, so the threshold is worth pinning.
        var notifier = new CapturingNotifier();
        var alerts = new FakeAlertsApi([
            Snapshot("AL-LOS014-SIGNAL", "LOS-T-014", "Lagos West"),
            Snapshot("AL-LOS021-SIGNAL", "LOS-T-021", "Lagos West"),
            Snapshot("AL-LOS033-SIGNAL", "LOS-T-033", "Lagos West"),
        ]);

        await RunAsync(Builder(alerts, notifier), Alarm());

        notifier.Sent[0].Actions[1].Action.Should().Contain("regional power and backhaul");
    }

    [Fact]
    public async Task NonCriticalAlarm_DoesNotDispatchAFieldEngineer()
    {
        var notifier = new CapturingNotifier();

        await RunAsync(Builder(new FakeAlertsApi([]), notifier), Alarm(severity: "Warning"));

        notifier.Sent[0].Actions[0].Action.Should().Contain("monitoring ticket");
        notifier.Sent[0].Actions[0].Action.Should().NotContain("Dispatch");
    }

    private static AlertSnapshot Snapshot(string code, string tower, string region) =>
        new(code, "Critical", "Active", "Signal collapse", region, tower,
            Cause: "Suspected antenna misalignment",
            SubscribersAffected: 1200,
            Confidence: 0.8,
            RaisedAtUtc: new DateTime(2026, 5, 5, 8, 10, 0, DateTimeKind.Utc));

    private sealed class FakeAlertsApi(IReadOnlyList<AlertSnapshot> active) : IAlertsApi
    {
        public Task<IReadOnlyList<AlertSnapshot>> ListActiveAsync(CancellationToken _ = default) =>
            Task.FromResult(active);

        public Task<IReadOnlyList<AlertSnapshot>> ListAllAsync(CancellationToken _ = default) =>
            Task.FromResult(active);
    }

    private sealed class CapturingNotifier : IIncidentNotifier
    {
        public List<IncidentNotification> Sent { get; } = [];

        public Task NotifyAsync(IncidentNotification notification, CancellationToken _ = default)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    /// <summary>The agent's tools need an ISender; offline, the model never calls one.</summary>
    private sealed class UnusedSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> _, CancellationToken __ = default) =>
            throw new NotSupportedException("The deterministic chat client does not issue tool calls.");

        public Task Send<TRequest>(TRequest _, CancellationToken __ = default)
            where TRequest : IRequest =>
            throw new NotSupportedException("The deterministic chat client does not issue tool calls.");

        public Task<object?> Send(object _, CancellationToken __ = default) =>
            throw new NotSupportedException("The deterministic chat client does not issue tool calls.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> _, CancellationToken __ = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object _, CancellationToken __ = default) =>
            throw new NotSupportedException();
    }
}
