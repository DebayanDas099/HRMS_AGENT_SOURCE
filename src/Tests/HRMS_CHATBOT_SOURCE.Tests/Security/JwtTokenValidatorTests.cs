using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Security;

public class JwtTokenValidatorTests
{
    internal static readonly AppSettings Settings = new()
    {
        AdminPrivate = "MIICXAIBAAKBgQCyVG5uxSM2GBdwEssJ5m48BKLNQLMDYFcfzU5KZqLwHfbdAvEQUsterJRvLoeTFk9xGH/Wh1QfvUsQVuTCYW4dTOzzMW40cu/pIZ8KYyrC1XhtnyysYkayQBweg0KGH3actZiLOCudGv2Fnz4r9klme/3YMymj8UeLcPmCBWfRGQIDAQABAoGAfbiOfmNXFpzXcTiekeU1U/TEvcVeLwQtiAsapdNEDdpiHqAjSSnFnII4x0VbaTPyX74w6hJQGWw/Tk6kSfGcSbcngzNwBd2PmbsePbxjLm/PCrYpf9854lr4jfUHg4oPSksqWz87D8r5oTfzJhMg6AGqYSOYm9OJPzDnDA58wfECQQDru6Hx1momexTlmcDe+1dS06Ge/7EX20o9C6RS0HwTxQS4JQfw4G8MP+BwPtjjMirDQ2TRjWevkZzRy8b5mCUNAkEAwaliQNgKDZJWaSfoHJtLSnPCGo9GTC1leDUuAvVuB/oQodTAPu5SdGUXdtY+wUrjm/KL56SPkRwERjb75G2xPQJAWD5r+BDQucj3YJ+24IHsBXhtlwyWaZzQZJu4Drw2xlvJUXmjSFtrloVO6hXMsPf1pBTVZ9BsUP/MWYjT2llG/QJAZNDN4l3VFe2ZaFKrBcFeN5r9cCAoA14albJxin7D0gk/AVAk6F3etNMvnOC5eJyI0tU4OdW0G2GPZBIZnXfxLQJBAMHXe/zqiiM0AwydTJzQumoMKTyGbrWPbj+QjLXcmkBz4rxt7LvicVFV/gODB246llr9qgw7LcC7azNda3Argrc=",
        EncryptionKey = "M5GvWhA7yIf7otcR77Lw3zAknLhBc4z",
        JwtIssuer = string.Empty,
        JwtIsValidateAudience = false,
        JwtIsValidateIssuer = false,
        JwtAccessTokenExpiryInMin = 480
    };

    [Fact]
    public void TryValidate_AcceptsTokenIssuedByJwtTokenService()
    {
        var token = GenerateToken();
        var validator = new JwtTokenValidator(Options.Create(Settings));

        var principal = validator.TryValidate(token.AccessToken);

        Assert.NotNull(principal);
        Assert.Equal("DG123", principal!.FindFirst("UserId")?.Value);
        Assert.Equal("1234567890", principal.FindFirst("Mobile")?.Value);
    }

    [Fact]
    public void TryValidate_RejectsTokenWithoutAudienceSuffix()
    {
        var token = GenerateToken();
        var validator = new JwtTokenValidator(Options.Create(Settings));
        var suffixIndex = token.AccessToken.IndexOf("|@|", StringComparison.Ordinal);
        var bareJwt = suffixIndex > 0 ? token.AccessToken[..suffixIndex] : token.AccessToken;

        Assert.Null(validator.TryValidate(bareJwt));
    }

    [Fact]
    public void TryValidate_RejectsTamperedToken()
    {
        var token = GenerateToken();
        var validator = new JwtTokenValidator(Options.Create(Settings));
        var suffixIndex = token.AccessToken.IndexOf("|@|", StringComparison.Ordinal);
        var tampered = suffixIndex > 0
            ? token.AccessToken[..(suffixIndex - 1)] + "x" + token.AccessToken[(suffixIndex - 1)..]
            : token.AccessToken + "x";

        Assert.Null(validator.TryValidate(tampered));
    }

    internal static GeneratedAccessToken GenerateToken()
    {
        var tokenService = new JwtTokenService(Options.Create(Settings));
        return tokenService.GenerateAccessToken(new AuthenticatedAdminUser
        {
            UserId = "DG123",
            FullName = "Test User",
            GroupCode = "GRP1",
            Department = "HR",
            Mobile = "1234567890",
            Email = "test@example.com",
            AdminYn = "Y",
            Active = "Y"
        }, rememberMe: false);
    }
}
