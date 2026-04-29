using Application.Abstractions.Messaging;
using Modules.Identity.Domain;
using Modules.Identity.Domain.RefreshTokens;
using Modules.Identity.Domain.Users;
using SharedKernel;

namespace Modules.Identity.Application.Authentication.Login;

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenService tokens)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        User? user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<LoginResponse>(UserErrors.AccountInactive);
        }

        TokenPair pair = tokens.Issue(user);
        await refreshTokens.AddAsync(
            RefreshToken.Issue(user.Id, tokens.HashRefreshToken(pair.RefreshToken), pair.RefreshExpiresAtUtc),
            cancellationToken);

        user.RecordLogin();
        await uow.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            pair.AccessToken,
            pair.RefreshToken,
            pair.AccessExpiresAtUtc,
            pair.RefreshExpiresAtUtc,
            new UserDto(user.Id, user.Email, user.FullName, user.Handle, user.Role, user.Team, user.Region));
    }
}
