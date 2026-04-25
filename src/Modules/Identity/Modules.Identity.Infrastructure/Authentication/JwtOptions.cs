namespace Modules.Identity.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "telcopilot";
    public string Audience { get; init; } = "telcopilot.api";
    public string Secret { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}
