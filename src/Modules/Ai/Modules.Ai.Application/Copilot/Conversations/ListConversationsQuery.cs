using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Copilot.Conversations;

public sealed record ListConversationsQuery(Guid UserId, int Take = 50) : IQuery<IReadOnlyList<ConversationSummary>>;

public sealed record ConversationSummary(
    Guid Id,
    string Title,
    int MessageCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastMessageAtUtc);
