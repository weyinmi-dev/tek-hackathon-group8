using MediatR;
using Modules.Identity.Application.Authentication.Login;
using Modules.Identity.Application.Authentication.Refresh;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

public sealed class Refresh : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/refresh", async (Request request, ISender sender, CancellationToken ct) =>
        {
            Result<LoginResponse> result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Auth)
        .AllowAnonymous();
    }

    public sealed record Request(string RefreshToken);
}
