using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// A tool's Deep Linking response (Deep Linking 2.0, sections 4.5 and 3.3, with the example response of the
/// specification): accepted only when signed by the tool's key, issued by its client id for the platform, current,
/// carrying the platform's own data and a fresh nonce, and of the tool's deployment; only <c>ltiResourceLink</c> items
/// are taken.
/// </summary>
public sealed class LtiDeepLinkingServiceTests
{
    private const string Issuer = "https://lms.example.com";

    private static readonly DateTime _now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly RSA _toolKey = RSA.Create(2048);
    private static readonly RSA _otherKey = RSA.Create(2048);

    private readonly TestClock _clock = new(_now);
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly ExternalTool _tool = new()
    {
        ItemId = "tool-1",
        ClientId = "client-1",
        DeploymentId = "deployment-1",
        Enabled = true,
        SubjectSalt = LtiSubjects.NewSalt(),
    };

    [Fact]
    public async Task ReadResponseAsync_TheSpecificationsExample_TakesTheResourceLinksAndReportsTheOtherTypes()
    {
        // Arrange
        var service = CreateService();
        var jwt = Response(items: new JsonArray(
            new JsonObject
            {
                ["type"] = "ltiResourceLink",
                ["title"] = "A title",
                ["text"] = "This is a link to an activity that will be graded",
                ["url"] = "https://lti.example.com/launchMe",
                ["icon"] = new JsonObject { ["url"] = "https://lti.example.com/image.jpg", ["width"] = 100, ["height"] = 100 },
                ["lineItem"] = new JsonObject { ["scoreMaximum"] = 87, ["label"] = "Chapter 12 quiz", ["resourceId"] = "xyzpdq1234", ["tag"] = "originality" },
                ["custom"] = new JsonObject { ["quiz_id"] = "az-123", ["duedate"] = "$Resource.submission.endDateTime" },
            },
            new JsonObject { ["type"] = "link", ["url"] = "https://www.youtube.com/watch?v=corV3-WsIro" },
            new JsonObject { ["type"] = "html", ["html"] = "<h1>A Custom Title</h1>" }));

        // Act
        var response = await service.ReadResponseAsync(jwt, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Succeeded, response.Error);
        Assert.Equal("course-1", response.Data.CourseId);
        Assert.Equal("user-1", response.Data.UserId);
        Assert.Equal(["link", "html"], response.RefusedTypes);

        var item = Assert.Single(response.Items);
        Assert.Equal("A title", item.Title);
        Assert.Equal("This is a link to an activity that will be graded", item.Text);
        Assert.Equal("https://lti.example.com/launchMe", item.Url);
        Assert.Equal(87, item.ScoreMaximum);
        Assert.Equal("Chapter 12 quiz", item.LineItemLabel);
        Assert.Equal("xyzpdq1234", item.LineItemResourceId);
        Assert.Equal("originality", item.LineItemTag);
        Assert.Equal("quiz_id=az-123\nduedate=$Resource.submission.endDateTime", item.CustomParameters);
    }

    [Fact]
    public async Task ReadResponseAsync_AResourceLinkWithoutALineItem_HasNoMaximum()
    {
        // Act
        var response = await CreateService().ReadResponseAsync(Response(items: new JsonArray(new JsonObject { ["type"] = "ltiResourceLink" })), TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(response.Items);
        Assert.Null(item.ScoreMaximum);
        Assert.Null(item.Url);
    }

    [Fact]
    public async Task ReadResponseAsync_SignedByAnotherKey_IsRefused()
    {
        // Act
        var response = await CreateService().ReadResponseAsync(Response(key: _otherKey), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Succeeded);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ReadResponseAsync_ForAnotherAudience_IsRefused()
        => Assert.False((await CreateService().ReadResponseAsync(Response(audience: "https://elsewhere.example.com"), TestContext.Current.CancellationToken)).Succeeded);

    [Fact]
    public async Task ReadResponseAsync_FromAClientThatIsNotATool_IsRefused()
        => Assert.False((await CreateService().ReadResponseAsync(Response(issuer: "client-2"), TestContext.Current.CancellationToken)).Succeeded);

    [Fact]
    public async Task ReadResponseAsync_AfterItExpired_IsRefused()
    {
        // Arrange
        var jwt = Response();

        _clock.UtcNow = _now.AddMinutes(10);

        // Act and assert
        Assert.False((await CreateService().ReadResponseAsync(jwt, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task ReadResponseAsync_OfAnotherDeployment_IsRefused()
        => Assert.False((await CreateService().ReadResponseAsync(Response(deploymentId: "deployment-2"), TestContext.Current.CancellationToken)).Succeeded);

    [Fact]
    public async Task ReadResponseAsync_NotADeepLinkingResponse_IsRefused()
        => Assert.False((await CreateService().ReadResponseAsync(Response(messageType: LtiConstants.MessageTypes.ResourceLinkRequest), TestContext.Current.CancellationToken)).Succeeded);

    [Fact]
    public async Task ReadResponseAsync_WithoutTheData_OrWithDataTheToolMadeUp_IsRefused()
    {
        // Act
        var missing = await CreateService().ReadResponseAsync(Response(data: string.Empty), TestContext.Current.CancellationToken);
        var forged = await CreateService().ReadResponseAsync(Response(data: "made-up"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(missing.Succeeded);
        Assert.False(forged.Succeeded);
    }

    [Fact]
    public async Task ReadResponseAsync_DataMadeForAnotherTool_IsRefused()
    {
        // Arrange
        var data = new LtiDeepLinkingDataTokens(_protection, _clock).Create(new LtiDeepLinkingData { ToolId = "tool-2", UserId = "user-1", CourseId = "course-1" });

        // Act and assert
        Assert.False((await CreateService().ReadResponseAsync(Response(data: data), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task ReadResponseAsync_AReplayedNonce_IsRefused()
    {
        // Arrange
        var service = CreateService();
        var first = await service.ReadResponseAsync(Response(nonce: "nonce-1"), TestContext.Current.CancellationToken);

        // Act
        var replay = await CreateService().ReadResponseAsync(Response(nonce: "nonce-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded, first.Error);
        Assert.False(replay.Succeeded);
    }

    [Fact]
    public async Task ReadResponseAsync_TheSameDataTwice_IsRefusedTheSecondTime()
    {
        // Arrange
        var data = Data();
        var first = await CreateService().ReadResponseAsync(Response(data: data), TestContext.Current.CancellationToken);

        // Act: a new nonce, the same request.
        var again = await CreateService().ReadResponseAsync(Response(data: data), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded, first.Error);
        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task ReadResponseAsync_KeepsTheToolsMessages()
    {
        // Act
        var response = await CreateService().ReadResponseAsync(Response(message: "Two items picked."), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Two items picked.", response.Message);
    }

    private LtiDeepLinkingService CreateService()
        => new(new KeyedToolDirectory(_tool), new Platform(), new LtiNonceStore(_cache), new LtiDeepLinkingDataTokens(_protection, _clock), _clock, NullLogger<LtiDeepLinkingService>.Instance);

    private string Data()
        => new LtiDeepLinkingDataTokens(_protection, _clock).Create(new LtiDeepLinkingData { ToolId = "tool-1", UserId = "user-1", CourseId = "course-1" });

    private string Response(
        RSA key = null,
        string issuer = "client-1",
        string audience = Issuer,
        string deploymentId = "deployment-1",
        string messageType = LtiConstants.MessageTypes.DeepLinkingResponse,
        string data = null,
        string nonce = null,
        string message = null,
        JsonArray items = null)
    {
        var claims = new Dictionary<string, object>
        {
            [LtiConstants.Claims.MessageType] = messageType,
            [LtiConstants.Claims.Version] = "1.3.0",
            [LtiConstants.Claims.DeploymentId] = deploymentId,
            ["nonce"] = nonce ?? Guid.NewGuid().ToString("n"),
            [LtiConstants.Claims.ContentItems] = System.Text.Json.JsonSerializer.SerializeToElement(items ?? new JsonArray(new JsonObject { ["type"] = "ltiResourceLink", ["title"] = "Simulator" })),
        };

        if (data != string.Empty)
        {
            claims[LtiConstants.Claims.Data] = data ?? Data();
        }

        if (message is not null)
        {
            claims[LtiConstants.Claims.Msg] = message;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = _now,
            NotBefore = _now,
            Expires = _now.AddMinutes(5),
            Claims = claims,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key ?? _toolKey) { KeyId = "tool-key-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private sealed class KeyedToolDirectory : ILtiToolDirectory
    {
        private readonly ExternalTool _tool;

        public KeyedToolDirectory(ExternalTool tool)
        {
            _tool = tool;
        }

        public Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(clientId == _tool.ClientId ? _tool : null);

        public Task<ExternalTool> FindByIdAsync(string toolId, CancellationToken cancellationToken = default)
            => Task.FromResult(toolId == _tool.ItemId ? _tool : null);

        public Task<bool> IsRedirectUriAllowedAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(ExternalTool tool, bool refresh = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SecurityKey>>([new RsaSecurityKey(_toolKey.ExportParameters(false)) { KeyId = "tool-key-1" }]);
    }

    private sealed class Platform : ILtiPlatform
    {
        public Task<LtiPlatformInfo> GetAsync()
            => Task.FromResult(new LtiPlatformInfo { Issuer = Issuer });
    }
}
