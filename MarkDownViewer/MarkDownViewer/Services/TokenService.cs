using System.Security.Claims;
using System.Text;
using MarkDownViewer.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace MarkDownViewer.Services;

public sealed class TokenService
{
    public const string AuthenticationScheme = "ApiToken";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);
    private readonly IDataProtector _protector;

    public TokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("MarkDownViewer.AuthToken");
    }

    public LoginResponse CreateLoginResponse(string userName)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var payload = $"{userName}\n{expiresAt:O}";
        var protectedPayload = _protector.Protect(payload);
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedPayload));
        return new LoginResponse(token, userName, expiresAt);
    }

    public ClaimsPrincipal? ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var protectedPayload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var payload = _protector.Unprotect(protectedPayload);
            var parts = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[1], out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, parts[0])
            };

            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }
}
