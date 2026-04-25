using Application.Abstractions.Messaging;
using Modules.Identity.Application.Authentication.Login;
using Modules.Identity.Domain.RefreshTokens;
using Modules.Identity.Domain.Users;
using SharedKernel;

namespace Modules.Identity.Application.Authentication.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IJwtTokenService tokens)
    : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        string hash = tokens.HashRefreshToken(request.RefreshToken);
        RefreshToken? existing = await refreshTokens.GetByHashAsync(hash, cancellationToken);
        if (existing is null || !existing.IsActive)
        {
            return Result.Failure<LoginResponse>(UserErrors.RefreshTokenInvalid);
        }

        User? user = await users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(UserErrors.NotFound);
        }

        // Rotate: revoke old, issue new pair
        existing.Revoke();
        TokenPair pair = tokens.Issue(user);
        await refreshTokens.AddAsync(
            RefreshToken.Issue(user.Id, tokens.HashRefreshToken(pair.RefreshToken), pair.RefreshExpiresAtUtc),
            cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            pair.AccessToken,
            pair.RefreshToken,
            pair.AccessExpiresAtUtc,
            pair.RefreshExpiresAtUtc,
            new UserDto(user.Id, user.Email, user.FullName, user.Handle, user.Role, user.Team, user.Region));
    }
}
