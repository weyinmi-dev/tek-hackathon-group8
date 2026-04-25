using Modules.Identity.Api;
using Modules.Identity.Domain.Users;

namespace Modules.Identity.Infrastructure.Api;

internal sealed class IdentityApi(IUserRepository users) : IIdentityApi
{
    public async Task<IdentityUserSnapshot?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        User? u = await users.GetByIdAsync(userId, ct);
        return u is null ? null : Map(u);
    }

    public async Task<IReadOnlyList<IdentityUserSnapshot>> ListUsersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<User> all = await users.ListAsync(ct);
        return all.Select(Map).ToList();
    }

    private static IdentityUserSnapshot Map(User u) =>
        new(u.Id, u.Email, u.FullName, u.Handle, u.Role, u.Team, u.Region, u.CreatedAtUtc, u.LastLoginAtUtc);
}
