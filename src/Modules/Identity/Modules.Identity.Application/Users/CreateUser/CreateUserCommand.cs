using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.CreateUser;

/// <summary>
/// Admin / Manager command to create a new user. Managers cannot create Admin
/// accounts (enforced in the handler against <see cref="ActorRole"/>).
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    string Handle,
    string Role,
    string Team,
    string Region,
    string ActorRole) : ICommand<CreatedUserDto>;

public sealed record CreatedUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Handle,
    string Role,
    string Team,
    string Region,
    bool IsActive);
