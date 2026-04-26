using SharedKernel;

namespace Modules.Identity.Domain.RefreshTokens;

public sealed class RefreshToken : Entity
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAtUtc) =>
        new(Guid.NewGuid(), userId, tokenHash, expiresAtUtc);

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
