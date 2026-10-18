using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Nomax.IntegrationTests.Support;

/// <summary>
/// Issues Keycloak-shaped access tokens signed with a local key and makes the hosts trust that key instead of
/// downloading the realm's JWKS (docs/09 §1: no test calls the internet).
/// </summary>
internal static class TestJwt
{
    public const string Issuer = "https://keycloak.test/realms/nomax";
    public const string Audience = "nomax-api";

    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-key" };
    private static readonly RsaSecurityKey ForeignKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string For(Guid userId, params string[] realmRoles) => Create(userId, realmRoles);

    public static string Create(
        Guid? userId,
        IEnumerable<string> realmRoles,
        string audience = Audience,
        DateTime? expires = null,
        bool signedByForeignKey = false)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var claims = new Dictionary<string, object>
        {
            ["realm_access"] = new Dictionary<string, object> { ["roles"] = realmRoles.Append("offline_access").ToArray() },
            ["preferred_username"] = "tester",
            ["azp"] = "nomax-web",
        };
        if (userId is { } id)
        {
            claims["sub"] = id.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = expiresAt.AddMinutes(-5),
            NotBefore = expiresAt.AddMinutes(-5),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signedByForeignKey ? ForeignKey : SigningKey, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static void TrustTestKey(IServiceCollection services) =>
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(SigningKey);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        });
}
