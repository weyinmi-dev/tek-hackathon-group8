using Application.Abstractions.Messaging;

namespace Modules.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessExpiresAtUtc,
    DateTime RefreshExpiresAtUtc,
    UserDto User);

public sealed record UserDto(Guid Id, string Email, string FullName, string Handle, string Role, string Team, string Region);
