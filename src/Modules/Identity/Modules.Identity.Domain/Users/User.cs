using SharedKernel;

namespace Modules.Identity.Domain.Users;

public sealed class User : Entity
{
    private User(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        string handle,
        string role,
        string team,
        string region)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Handle = handle;
        Role = role;
        Team = team;
        Region = region;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private User() { }

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Handle { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public string Team { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public static Result<User> Create(
        string email,
        string passwordHash,
        string fullName,
        string handle,
        string role,
        string team,
        string region)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Failure<User>(UserErrors.EmailRequired);
        if (string.IsNullOrWhiteSpace(passwordHash)) return Result.Failure<User>(UserErrors.PasswordRequired);
        if (!Roles.IsValid(role)) return Result.Failure<User>(UserErrors.RoleInvalid);

        var user = new User(Guid.NewGuid(), email.Trim().ToLowerInvariant(), passwordHash, fullName, handle, role.ToLowerInvariant(), team, region);
        user.Raise(new UserCreatedDomainEvent(user.Id, user.Email, user.Role));
        return user;
    }

    public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;
}
