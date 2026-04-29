using Application.Abstractions.Messaging;
using Modules.Identity.Domain;
using Modules.Identity.Domain.Users;
using SharedKernel;
using Roles = Modules.Identity.Application.Authorization.Roles;

namespace Modules.Identity.Application.Users.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand cmd, CancellationToken ct)
    {
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

        if (!string.Equals(user.Handle, cmd.Handle, StringComparison.Ordinal))
        {
            User? clash = await users.GetByHandleAsync(cmd.Handle, ct);
            if (clash is not null && clash.Id != user.Id)
            {
                return Result.Failure(UserErrors.HandleAlreadyExists);
            }
        }

        Result update = user.UpdateProfile(cmd.FullName, cmd.Handle, cmd.Team, cmd.Region);
        if (update.IsFailure)
        {
            return update;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
