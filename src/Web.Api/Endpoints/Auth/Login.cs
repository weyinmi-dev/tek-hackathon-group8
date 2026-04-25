using MediatR;
using Modules.Identity.Application.Authentication.Login;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

public sealed class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/login", async (Request request, ISender sender, CancellationToken ct) =>
        {
            Result<LoginResponse> result = await sender.Send(new LoginCommand(request.Email, request.Password), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Auth)
        .AllowAnonymous();
    }

    public sealed record Request(string Email, string Password);
}
