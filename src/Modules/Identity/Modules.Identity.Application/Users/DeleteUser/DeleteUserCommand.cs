using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Users.DeleteUser;

/// <summary>Admin-only hard delete. Removes the user row outright; their audit history is retained.</summary>
public sealed record DeleteUserCommand(Guid UserId, Guid ActorId) : ICommand;
