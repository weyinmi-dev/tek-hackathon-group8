using Microsoft.EntityFrameworkCore;
using Modules.Ai.Domain.Conversations;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Repositories;

internal sealed class ConversationRepository(AiDbContext db) : IConversationRepository
{
    public Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // AsSplitQuery so the message backfill doesn't blow up the cartesian product
        // when a conversation grows large. Ordering on the navigation drives the UI replay.
        return await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAtUtc))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        await db.Conversations.AddAsync(conversation, cancellationToken);

    public void Remove(Conversation conversation) => db.Conversations.Remove(conversation);

    public async Task<IReadOnlyList<Conversation>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
        await db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Conversations.CountAsync(c => c.UserId == userId, cancellationToken);
}
