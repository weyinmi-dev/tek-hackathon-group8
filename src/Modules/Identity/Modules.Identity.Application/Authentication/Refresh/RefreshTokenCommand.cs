using Application.Abstractions.Messaging;
using Modules.Identity.Application.Authentication.Login;

namespace Modules.Identity.Application.Authentication.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResponse>;
