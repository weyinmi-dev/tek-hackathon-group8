using SharedKernel;

namespace Modules.Identity.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailRequired = Error.Problem("User.EmailRequired", "Email is required.");
    public static readonly Error PasswordRequired = Error.Problem("User.PasswordRequired", "Password is required.");
    public static readonly Error PasswordTooShort = Error.Problem("User.PasswordTooShort", "Password must be at least 8 characters.");
    public static readonly Error RoleInvalid = Error.Problem("User.RoleInvalid", "Role must be one of: engineer, manager, admin, viewer.");
    public static readonly Error FullNameRequired = Error.Problem("User.FullNameRequired", "Full name is required.");
    public static readonly Error HandleRequired = Error.Problem("User.HandleRequired", "Handle is required.");
    public static readonly Error EmailAlreadyExists = Error.Conflict("User.EmailAlreadyExists", "A user with that email already exists.");
    public static readonly Error HandleAlreadyExists = Error.Conflict("User.HandleAlreadyExists", "A user with that handle already exists.");
    public static readonly Error CannotManageAdmin = Error.Forbidden("User.CannotManageAdmin", "Managers cannot create or modify Admin accounts.");
    public static readonly Error CannotDemoteSelf = Error.Forbidden("User.CannotDemoteSelf", "You cannot change your own role.");
    public static readonly Error CannotDeactivateSelf = Error.Forbidden("User.CannotDeactivateSelf", "You cannot deactivate your own account.");
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");
    public static readonly Error InvalidCredentials = Error.Problem("User.InvalidCredentials", "Email or password is incorrect.");
    public static readonly Error RefreshTokenInvalid = Error.Problem("User.RefreshTokenInvalid", "Refresh token is invalid or expired.");
    public static readonly Error AccountInactive = Error.Forbidden("User.AccountInactive", "This account is deactivated.");
}
