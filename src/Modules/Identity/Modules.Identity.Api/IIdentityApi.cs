namespace Modules.Identity.Api;

public sealed record IdentityUserSnapshot(
    Guid Id,
    string Email,
    string FullName,
    string Handle,
    string Role,
    string Team,
    string Region,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

public interface IIdentityApi
{
    Task<IdentityUserSnapshot?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdentityUserSnapshot>> ListUsersAsync(CancellationToken cancellationToken = default);
}
