using Application.Abstractions.Messaging;
using Modules.Identity.Domain;
using Modules.Identity.Domain.Users;
using SharedKernel;
using Roles = Modules.Identity.Application.Authorization.Roles;

namespace Modules.Identity.Application.Users.ChangeRole;

internal sealed class ChangeUserRoleCommandHandler(
    IUserRepository users,
    IUnitOfWork uow) : ICommandHandler<ChangeUserRoleCommand>
{
    public async Task<Result> Handle(ChangeUserRoleCommand cmd, CancellationToken ct)
    {
        if (cmd.UserId == cmd.ActorId)
        {
            return Result.Failure(UserErrors.CannotDemoteSelf);
        }

        User? user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        bool actorIsManager = string.Equals(cmd.ActorRole, Roles.Manager, StringComparison.OrdinalIgnoreCase);
        bool targetingAdmin = string.Equals(user.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase);
        bool elevatingToAdmin = string.Equals(cmd.NewRole, Roles.Admin, StringComparison.OrdinalIgnoreCase);

        if (actorIsManager && (targetingAdmin || elevatingToAdmin))
        {
            return Result.Failure(UserErrors.CannotManageAdmin);
        }

        Result change = user.ChangeRole(cmd.NewRole);
        if (change.IsFailure)
        {
            return change;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
