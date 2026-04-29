using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

internal sealed class ListConversationsQueryHandler(IConversationRepository conversations)
    : IQueryHandler<ListConversationsQuery, IReadOnlyList<ConversationSummary>>
{
    public async Task<Result<IReadOnlyList<ConversationSummary>>> Handle(ListConversationsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Conversation> rows = await conversations.ListForUserAsync(request.UserId, request.Take, cancellationToken);
        IReadOnlyList<ConversationSummary> items = rows
            .Select(c => new ConversationSummary(c.Id, c.Title, c.MessageCount, c.CreatedAtUtc, c.UpdatedAtUtc, c.LastMessageAtUtc))
            .ToList();
        return Result.Success(items);
    }
}
