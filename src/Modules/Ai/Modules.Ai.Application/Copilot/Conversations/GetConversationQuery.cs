using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Conversations;

namespace Modules.Ai.Application.Copilot.Conversations;

public sealed record GetConversationQuery(Guid ConversationId, Guid UserId) : IQuery<ConversationDetail>;

public sealed record ConversationDetail(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<MessageView> Messages);

public sealed record MessageView(
    Guid Id,
    MessageRole Role,
    string Content,
    string? Metadata,
    DateTime CreatedAtUtc);
