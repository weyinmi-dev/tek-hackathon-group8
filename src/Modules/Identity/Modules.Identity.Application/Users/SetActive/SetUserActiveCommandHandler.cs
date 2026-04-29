using Application.Abstractions.Messaging;
using Modules.Identity.Domain;
using Modules.Identity.Domain.Users;
using SharedKernel;
using Roles = Modules.Identity.Application.Authorization.Roles;

namespace Modules.Identity.Application.Users.SetActive;

internal sealed class SetUserActiveCommandHandler(
    IUserRepository users,
    IUnitOfWork uow) : ICommandHandler<SetUserActiveCommand>
{
    public async Task<Result> Handle(SetUserActiveCommand cmd, CancellationToken ct)
    {
        if (cmd.UserId == cmd.ActorId && !cmd.IsActive)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (string.Equals(cmd.ActorRole, Roles.Manager, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(user.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(UserErrors.CannotManageAdmin);
        }

        if (cmd.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
