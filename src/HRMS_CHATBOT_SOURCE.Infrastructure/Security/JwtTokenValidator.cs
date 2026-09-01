using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Security;

public sealed class JwtTokenValidator
{
    private readonly AppSettings _appSettings;
    private readonly RsaSecurityKey _validationKey;

    public JwtTokenValidator(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings.Value;
        _validationKey = CreateValidationKey(_appSettings.AdminPrivate);
    }

    public ClaimsPrincipal? TryValidate(HttpContext context)
    {
        var rawToken = ReadRawToken(context);
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        return TryValidate(rawToken);
    }

    public ClaimsPrincipal? TryValidate(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var parts = rawToken.Split("|@|", StringSplitOptions.None);
        if (parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1]))
        {
            return null;
        }

        var accessToken = parts[0];
        var encryptionKey = _appSettings.EncryptionKey
            ?? throw new InvalidOperationException("AppSettings:EncryptionKey is not configured.");
        var audience = Encryption.Decrypt(parts[1], encryptionKey);
        if (string.IsNullOrWhiteSpace(audience))
        {
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = _appSettings.JwtIssuer,
            ValidateIssuer = _appSettings.JwtIsValidateIssuer,
            ValidAudience = audience,
            ValidateAudience = _appSettings.JwtIsValidateAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _validationKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        try
        {
            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken
                || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public static string? ReadRawToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("hrms_admin_token", out var headerToken)
            && !string.IsNullOrWhiteSpace(headerToken))
        {
            return headerToken.ToString().Trim();
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            var value = authorization.ToString().Trim();
            return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? value[7..].Trim()
                : value;
        }

        if (context.Request.Cookies.TryGetValue("hrms_admin_token", out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken.Trim();
        }

        return null;
    }

    private static RsaSecurityKey CreateValidationKey(string? adminPrivate)
    {
        if (string.IsNullOrWhiteSpace(adminPrivate))
        {
            throw new InvalidOperationException("AppSettings:AdminPrivate is not configured.");
        }

        byte[] privateKeyRaw = Convert.FromBase64String(adminPrivate);
        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyRaw, out _);
        return new RsaSecurityKey(rsa.ExportParameters(false))
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }
}
