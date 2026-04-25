using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.ListUsers;

public sealed record ListUsersQuery() : IQuery<IReadOnlyList<UserListItem>>;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string FullName,
    string Handle,
    string Role,
    string Team,
    string Region,
    DateTime? LastLoginAtUtc);
