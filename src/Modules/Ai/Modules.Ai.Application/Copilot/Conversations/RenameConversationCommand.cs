using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Copilot.Conversations;

public sealed record RenameConversationCommand(Guid ConversationId, Guid UserId, string NewTitle) : ICommand;
