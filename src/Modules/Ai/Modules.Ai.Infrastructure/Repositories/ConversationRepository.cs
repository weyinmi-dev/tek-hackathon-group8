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

    public void Detach(Conversation conversation) => db.Entry(conversation).State = EntityState.Detached;

    public Task<int> UpdateActivityAsync(Guid id, int messageCount, DateTime updatedAtUtc, DateTime lastMessageAtUtc, CancellationToken cancellationToken = default) =>
        db.Conversations
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.MessageCount, messageCount)
                      .SetProperty(c => c.UpdatedAtUtc, updatedAtUtc)
                      .SetProperty(c => c.LastMessageAtUtc, lastMessageAtUtc),
                cancellationToken);

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
