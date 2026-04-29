using Microsoft.EntityFrameworkCore;
using Modules.Identity.Domain.Users;
using Modules.Identity.Infrastructure.Database;

namespace Modules.Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        // Emails are stored in canonical lowercase form (see User.Create). Normalize the
        // lookup the same way so plain == translates to a parameterized SQL equality
        // and uses the unique index on Email.
#pragma warning disable CA1308 // Normalize strings to uppercase

        string normalized = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        return db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public Task<User?> GetByHandleAsync(string handle, CancellationToken cancellationToken = default)
    {
        string normalized = handle.Trim();
        return db.Users.FirstOrDefaultAsync(u => u.Handle == normalized, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public void Remove(User user) => db.Users.Remove(user);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Users.CountAsync(ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
}
