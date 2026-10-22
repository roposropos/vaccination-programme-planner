using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "VaxPlan";
    public string Audience { get; set; } = "VaxPlan";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

public static class AppClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string PatientId = "patient_id";
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var settings = options.Value;
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, user.Id.ToString()),
            new(AppClaims.Email, user.Email),
            new(AppClaims.Name, user.DisplayName),
            new(AppClaims.Role, user.Role)
        };

        if (user.PatientId is { } patientId)
        {
            claims.Add(new Claim(AppClaims.PatientId, patientId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var expiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(settings.ExpiresMinutes);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: clock.GetUtcNow().UtcDateTime,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
