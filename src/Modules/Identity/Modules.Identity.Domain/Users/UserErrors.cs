using SharedKernel;

namespace Modules.Identity.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailRequired = Error.Problem("User.EmailRequired", "Email is required.");
    public static readonly Error PasswordRequired = Error.Problem("User.PasswordRequired", "Password is required.");
    public static readonly Error RoleInvalid = Error.Problem("User.RoleInvalid", "Role must be one of: engineer, manager, admin, viewer.");
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");
    public static readonly Error InvalidCredentials = Error.Problem("User.InvalidCredentials", "Email or password is incorrect.");
    public static readonly Error RefreshTokenInvalid = Error.Problem("User.RefreshTokenInvalid", "Refresh token is invalid or expired.");
}
