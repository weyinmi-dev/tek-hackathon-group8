using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

/// <summary>
/// Loads a conversation's messages in chronological order for the MAF chat-history provider
/// (Phase 2 §8.1), so the agent sees prior turns. Read-only; returns a flat DTO the provider
/// maps onto MAF <c>ChatMessage</c>s. Returns an empty list for an unknown conversation rather
/// than failing — a fresh session simply has no history yet.
/// </summary>
public sealed record GetConversationMessagesQuery(Guid ConversationId)
    : IQuery<IReadOnlyList<ConversationMessageDto>>;

public sealed record ConversationMessageDto(string Role, string Content, DateTime CreatedAtUtc);

internal sealed class GetConversationMessagesQueryHandler(IConversationRepository conversations)
    : IQueryHandler<GetConversationMessagesQuery, IReadOnlyList<ConversationMessageDto>>
{
    public async Task<Result<IReadOnlyList<ConversationMessageDto>>> Handle(
        GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        Conversation? conversation = await conversations.GetWithMessagesAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Success<IReadOnlyList<ConversationMessageDto>>([]);
        }

        IReadOnlyList<ConversationMessageDto> messages = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ConversationMessageDto(m.Role.ToString(), m.Content, m.CreatedAtUtc))
            .ToList();

        return Result.Success(messages);
    }
}
