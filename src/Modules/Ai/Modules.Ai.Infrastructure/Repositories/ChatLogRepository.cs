using Microsoft.EntityFrameworkCore;
using Modules.Ai.Domain.Conversations;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Repositories;

internal sealed class ChatLogRepository(AiDbContext db) : IChatLogRepository
{
    public async Task AddAsync(ChatLog log, CancellationToken cancellationToken = default) => await db.ChatLogs.AddAsync(log, cancellationToken);
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => db.ChatLogs.CountAsync(cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
