using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly AppSettings _appSettings;

    public JwtTokenService(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings.Value;
    }

    public GeneratedAccessToken GenerateAccessToken(AuthenticatedAdminUser user, bool rememberMe)
    {
        var expiryMinutes = rememberMe
            ? _appSettings.RememberMeDays * 24 * 60
            : _appSettings.JwtAccessTokenExpiryInMin;

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var audience = AppSettings.JwtAudienceAdmin;
        var secretKey = _appSettings.AdminPrivate
            ?? throw new InvalidOperationException("AppSettings:AdminPrivate is not configured.");

        var encryptionKey = _appSettings.EncryptionKey
            ?? throw new InvalidOperationException("AppSettings:EncryptionKey is not configured.");

        byte[] privateKeyRaw = Convert.FromBase64String(secretKey);

        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyRaw, out _);

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(AdminClaimFactory.CreateClaims(user)),
            Expires = expiresAt,
            Issuer = _appSettings.JwtIssuer ?? string.Empty,
            Audience = audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        var accessToken = string.Concat(jwt, "|@|", Encryption.Encrypt(audience, encryptionKey));

        return new GeneratedAccessToken
        {
            AccessToken = accessToken,
            ExpiresInSeconds = (int)TimeSpan.FromMinutes(expiryMinutes).TotalSeconds
        };
    }
}
