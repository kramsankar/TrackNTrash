using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrackNTrash.Tracking.Infrastructure;

namespace TrackNTrash.Tracking.Api.Auth;

public sealed record AuthOptions
{
    /// <summary>Signing key for locally-issued tokens. Set Auth:SigningKey in config/Key Vault.</summary>
    public string SigningKey { get; init; } = "";
    public string Issuer { get; init; } = "tracktrash";
    public string Audience { get; init; } = "tracktrash-console";
    public int LifetimeHours { get; init; } = 12;

    /// <summary>Entra ID tenant — when set, tokens from Azure AD are accepted too.</summary>
    public string? EntraTenantId { get; init; }
    /// <summary>Entra ID application (client) id / audience of the API.</summary>
    public string? EntraAudience { get; init; }

    public bool LocalEnabled => !string.IsNullOrWhiteSpace(SigningKey);
    public bool EntraEnabled => !string.IsNullOrWhiteSpace(EntraTenantId) && !string.IsNullOrWhiteSpace(EntraAudience);
}

/// <summary>Issues JWTs for local username/password sign-in.</summary>
public sealed class TokenService
{
    private readonly AuthOptions _opts;
    public TokenService(AuthOptions opts) => _opts = opts;

    public (string Token, DateTimeOffset ExpiresUtc) Issue(SqlUserStore.AppUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(_opts.LifetimeHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer, audience: _opts.Audience, claims: claims,
            notBefore: DateTime.UtcNow, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
