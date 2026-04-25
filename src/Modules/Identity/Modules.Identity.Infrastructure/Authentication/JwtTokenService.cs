using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Modules.Identity.Application.Authentication;
using Modules.Identity.Domain.Users;

namespace Modules.Identity.Infrastructure.Authentication;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _opt = options.Value;

    public TokenPair Issue(User user)
    {
        DateTime accessExpires = DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes);
        DateTime refreshExpires = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("handle", user.Handle),
            new("team", user.Team),
            new("region", user.Region),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: accessExpires,
            signingCredentials: creds);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        string refreshToken = GenerateRefreshToken();

        return new TokenPair(accessToken, refreshToken, accessExpires, refreshExpires);
    }

    public string HashRefreshToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
