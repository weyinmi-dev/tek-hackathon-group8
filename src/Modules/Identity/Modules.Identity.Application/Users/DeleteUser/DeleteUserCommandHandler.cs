using Application.Abstractions.Messaging;
using Modules.Identity.Domain;
using Modules.Identity.Domain.Users;
using SharedKernel;

namespace Modules.Identity.Application.Users.DeleteUser;

internal sealed class DeleteUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand cmd, CancellationToken ct)
    {
        if (cmd.UserId == cmd.ActorId)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        users.Remove(user);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
