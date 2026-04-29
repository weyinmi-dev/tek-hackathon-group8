using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Copilot.Conversations;

public sealed record DeleteConversationCommand(Guid ConversationId, Guid UserId) : ICommand;
