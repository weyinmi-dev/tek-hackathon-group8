using Modules.Identity.Domain.Users;

namespace Modules.Identity.Application.Authentication;

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime AccessExpiresAtUtc, DateTime RefreshExpiresAtUtc);

public interface IJwtTokenService
{
    TokenPair Issue(User user);
    string HashRefreshToken(string token);
}
