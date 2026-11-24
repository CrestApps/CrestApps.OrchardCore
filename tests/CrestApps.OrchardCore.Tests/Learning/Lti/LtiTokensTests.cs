using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// The platform signs its tokens as JWTs with the key's id; a tool's token is accepted only with the tool's key, the
/// tool as issuer, the platform as audience and a current <c>exp</c> and <c>iat</c>. The login initiation URL carries the
/// parameters LTI 1.3 Core 5.1.1 lists.
/// </summary>
public sealed class LtiTokensTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static readonly RSA _toolKey = RSA.Create(2048);
    private static readonly RSA _otherKey = RSA.Create(2048);

    [Fact]
    public void Sign_WritesAJwtWithTheKeysId_AndJsonClaimsAsObjects()
    {
        // Arrange
        var credentials = new SigningCredentials(new RsaSecurityKey(_toolKey) { KeyId = "lti-1" }, SecurityAlgorithms.RsaSha256);

        // Act
        var token = new JsonWebToken(LtiTokens.Sign(new Dictionary<string, object>
        {
            ["iss"] = "https://lms.example.com",
            ["context"] = new JsonObject { ["id"] = "course-1" },
            ["roles"] = new JsonArray("a", "b"),
        }, credentials));

        // Assert
        Assert.Equal("lti-1", token.Kid);
        Assert.Equal("RS256", token.Alg);
        Assert.Equal("JWT", token.Typ);
        Assert.Equal("https://lms.example.com", token.Issuer);
        Assert.Equal("course-1", JsonNode.Parse(token.GetClaim("context").Value)?["id"]?.GetValue<string>());
        Assert.Equal(2, token.GetPayloadValue<string[]>("roles").Length);
    }

    [Fact]
    public async Task ValidateAsync_AToolsTokenWithItsKey_IsValid()
    {
        // Act
        var result = await LtiTokens.ValidateAsync(ToolToken(_toolKey), "client-1", "https://lms.example.com", [new RsaSecurityKey(_toolKey.ExportParameters(false))], _now);

        // Assert
        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task ValidateAsync_ATokenSignedByAnotherKey_IsRefused()
    {
        // Act
        var result = await LtiTokens.ValidateAsync(ToolToken(_otherKey), "client-1", "https://lms.example.com", [new RsaSecurityKey(_toolKey.ExportParameters(false))], _now);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("client-2", "https://lms.example.com", 0)]
    [InlineData("client-1", "https://elsewhere.example.com", 0)]
    [InlineData("client-1", "https://lms.example.com", 10)]
    public async Task ValidateAsync_AWrongIssuerAudienceOrAnExpiredToken_IsRefused(string issuer, string audience, int minutesLater)
    {
        // Act
        var result = await LtiTokens.ValidateAsync(ToolToken(_toolKey), issuer, audience, [new RsaSecurityKey(_toolKey.ExportParameters(false))], _now.AddMinutes(minutesLater));

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ATokenWithoutIat_IsRefused()
    {
        // Arrange
        var token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "client-1",
            Audience = "https://lms.example.com",
            Expires = _now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_toolKey), SecurityAlgorithms.RsaSha256),
        });

        // Act
        var result = await LtiTokens.ValidateAsync(token, "client-1", "https://lms.example.com", [new RsaSecurityKey(_toolKey.ExportParameters(false))], _now);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void BuildUrl_CarriesTheLoginInitiationParameters()
    {
        // Arrange
        var tool = new ExternalTool
        {
            ClientId = "client 1",
            DeploymentId = "deployment-1",
            LoginInitiationUrl = "https://tool.example.com/login?tenant=a",
        };

        var platform = new LtiPlatformInfo { Issuer = "https://lms.example.com" };

        // Act
        var url = new Uri(LtiLoginInitiation.BuildUrl(tool, platform, "subject-1", "hint+/=", "https://tool.example.com/launch"));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Query);

        // Assert
        Assert.Equal("tool.example.com", url.Host);
        Assert.Equal("/login", url.AbsolutePath);
        Assert.Equal("a", query["tenant"]);
        Assert.Equal("https://lms.example.com", query["iss"]);
        Assert.Equal("subject-1", query["login_hint"]);
        Assert.Equal("hint+/=", query["lti_message_hint"]);
        Assert.Equal("https://tool.example.com/launch", query["target_link_uri"]);
        Assert.Equal("client 1", query["client_id"]);
        Assert.Equal("deployment-1", query["lti_deployment_id"]);
    }

    private static string ToolToken(RSA key)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "client-1",
            Audience = "https://lms.example.com",
            IssuedAt = _now,
            NotBefore = _now,
            Expires = _now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256),
        });
}
