using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.SetActive;

public sealed record SetUserActiveCommand(
    Guid UserId,
    bool IsActive,
    string ActorRole,
    Guid ActorId) : ICommand;
