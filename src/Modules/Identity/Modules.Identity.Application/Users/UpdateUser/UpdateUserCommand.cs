using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Handle,
    string Team,
    string Region,
    string ActorRole) : ICommand;
