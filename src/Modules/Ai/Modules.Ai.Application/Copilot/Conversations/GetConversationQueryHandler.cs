using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

internal sealed class GetConversationQueryHandler(IConversationRepository conversations)
    : IQueryHandler<GetConversationQuery, ConversationDetail>
{
    public async Task<Result<ConversationDetail>> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        Conversation? conv = await conversations.GetWithMessagesAsync(request.ConversationId, cancellationToken);
        if (conv is null || conv.UserId != request.UserId)
        {
            // Owner mismatch is treated as NotFound rather than Forbidden — avoids leaking
            // existence of other users' conversations to a determined attacker.
            return Result.Failure<ConversationDetail>(Error.NotFound("Conversation.NotFound", "Conversation not found."));
        }

        IReadOnlyList<MessageView> messages = conv.Messages
            .Select(m => new MessageView(m.Id, m.Role, m.Content, m.Metadata, m.CreatedAtUtc))
            .ToList();

        return Result.Success(new ConversationDetail(conv.Id, conv.Title, conv.CreatedAtUtc, conv.UpdatedAtUtc, messages));
    }
}
