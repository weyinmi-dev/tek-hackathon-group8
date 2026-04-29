using Application.Abstractions.Messaging;
using Modules.Identity.Application.Authentication;
using Modules.Identity.Domain;
using Modules.Identity.Domain.Users;
using SharedKernel;
using Roles = Modules.Identity.Application.Authorization.Roles;

namespace Modules.Identity.Application.Users.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher) : ICommandHandler<CreateUserCommand, CreatedUserDto>
{
    public async Task<Result<CreatedUserDto>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Password) || cmd.Password.Length < 8)
        {
            return Result.Failure<CreatedUserDto>(UserErrors.PasswordTooShort);
        }

        // Manager-tier privilege boundary: an account with role=Manager cannot mint an Admin.
        // The endpoint already gates on RequireManager+ for the call itself; this prevents
        // a Manager from elevating accounts beyond their own ceiling.
        if (string.Equals(cmd.ActorRole, Roles.Manager, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(cmd.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<CreatedUserDto>(UserErrors.CannotManageAdmin);
        }

        if (await users.GetByEmailAsync(cmd.Email, ct) is not null)
        {
            return Result.Failure<CreatedUserDto>(UserErrors.EmailAlreadyExists);
        }

        if (await users.GetByHandleAsync(cmd.Handle, ct) is not null)
        {
            return Result.Failure<CreatedUserDto>(UserErrors.HandleAlreadyExists);
        }

        string hash = hasher.Hash(cmd.Password);
        Result<User> created = User.Create(cmd.Email, hash, cmd.FullName, cmd.Handle, cmd.Role, cmd.Team, cmd.Region);
        if (created.IsFailure)
        {
            return Result.Failure<CreatedUserDto>(created.Error);
        }

        await users.AddAsync(created.Value, ct);
        await uow.SaveChangesAsync(ct);

        User u = created.Value;
        return Result.Success(new CreatedUserDto(u.Id, u.Email, u.FullName, u.Handle, u.Role, u.Team, u.Region, u.IsActive));
    }
}
