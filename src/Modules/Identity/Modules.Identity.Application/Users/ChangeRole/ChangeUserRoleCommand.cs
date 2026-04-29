using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.ChangeRole;

public sealed record ChangeUserRoleCommand(
    Guid UserId,
    string NewRole,
    string ActorRole,
    Guid ActorId) : ICommand;
