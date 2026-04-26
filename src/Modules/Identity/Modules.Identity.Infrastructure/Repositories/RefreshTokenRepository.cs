using Microsoft.EntityFrameworkCore;
using Modules.Identity.Domain.RefreshTokens;
using Modules.Identity.Infrastructure.Database;

namespace Modules.Identity.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}
