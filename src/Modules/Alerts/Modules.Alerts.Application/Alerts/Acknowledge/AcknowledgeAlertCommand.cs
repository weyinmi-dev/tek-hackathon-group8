using Application.Abstractions.Messaging;

namespace Modules.Alerts.Application.Alerts.Acknowledge;

public sealed record AcknowledgeAlertCommand(string AlertCode, string Actor) : ICommand;
