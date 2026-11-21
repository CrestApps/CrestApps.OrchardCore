using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core.Models;
using CrestApps.OrchardCore.SigningKeys.Core.Services;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using CrestApps.OrchardCore.Tests.SigningKeys;
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
/// The authorization step of the third-party initiated login (Security Framework 5.1.1.2 and 5.1.1.3): the request must be
/// an OpenID Connect implicit request with <c>id_token</c>, <c>form_post</c>, <c>prompt=none</c> and <c>openid</c>, from a
/// registered tool to one of its redirect URIs, for the signed-in person the signed message hint was made for, with a
/// nonce not seen before. The answer is an id token signed with the platform key, posted with the state.
/// </summary>
public sealed class LtiAuthorizationServiceTests
{
    private const string RedirectUri = "https://tool.example.com/lti/launch";

    private static readonly DateTime _now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestClock _clock = new(_now);
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly InMemoryDocumentManager<SigningKeysDocument> _keyDocuments = new();
    private readonly InMemorySecretManager _secrets = new();
    private readonly ExternalTool _tool = new()
    {
        ItemId = "tool-1",
        Name = "Simulator",
        ClientId = "client-1",
        DeploymentId = "deployment-1",
        LoginInitiationUrl = "https://tool.example.com/lti/login",
        TargetLinkUri = "https://tool.example.com/lti/launch",
        SubjectSalt = LtiSubjects.NewSalt(),
        Enabled = true,
    };

    [Fact]
    public async Task AuthorizeAsync_AValidRequest_PostsAnIdTokenSignedWithThePlatformKey()
    {
        // Arrange
        var service = CreateService();
        var request = Request();

        // Act
        var result = await service.AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.ErrorDescription);
        Assert.Equal(RedirectUri, result.RedirectUri);
        Assert.Equal("state-1", result.State);

        var jwks = await CreateKeys().GetJsonWebKeySetAsync(TestContext.Current.CancellationToken);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(result.IdToken, new TokenValidationParameters
        {
            ValidIssuer = "https://lms.example.com",
            ValidAudience = "client-1",
            IssuerSigningKeys = new JsonWebKeySet(jwks.ToJsonString()).GetSigningKeys(),
            LifetimeValidator = (_, _, _, _) => true,
        });

        Assert.True(validation.IsValid, validation.Exception?.Message);

        var token = new JsonWebToken(result.IdToken);
        Assert.Equal("nonce-1", token.GetClaim("nonce").Value);
        Assert.Equal(LtiSubjects.For(_tool, "user-1"), token.Subject);
        Assert.Equal(LtiConstants.MessageTypes.ResourceLinkRequest, token.GetClaim(LtiConstants.Claims.MessageType).Value);
    }

    [Theory]
    [InlineData("scope", "profile")]
    [InlineData("response_type", "code")]
    [InlineData("response_mode", "fragment")]
    [InlineData("prompt", "login")]
    [InlineData("nonce", "")]
    public async Task AuthorizeAsync_ARequestThatIsNotAnLtiLogin_PostsInvalidRequest(string parameter, string value)
    {
        // Arrange
        var request = Request();

        switch (parameter)
        {
            case "scope": request.Scope = value; break;
            case "response_type": request.ResponseType = value; break;
            case "response_mode": request.ResponseMode = value; break;
            case "prompt": request.Prompt = value; break;
            case "nonce": request.Nonce = value; break;
        }

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_request", result.Error);
        Assert.Equal(RedirectUri, result.RedirectUri);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task AuthorizeAsync_AnUnknownClient_PostsNothing()
    {
        // Arrange
        var request = Request();
        request.ClientId = "client-2";

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("unauthorized_client", result.Error);
        Assert.Null(result.RedirectUri);
    }

    [Fact]
    public async Task AuthorizeAsync_ARedirectUriTheToolDidNotRegister_PostsNothing()
    {
        // Arrange
        var request = Request();
        request.RedirectUri = "https://attacker.example.com/catch";

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
        Assert.Null(result.RedirectUri);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task AuthorizeAsync_NobodySignedIn_PostsLoginRequired()
    {
        // Act
        var result = await CreateService().AuthorizeAsync(Request(), userId: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("login_required", result.Error);
        Assert.Equal(RedirectUri, result.RedirectUri);
    }

    [Fact]
    public async Task AuthorizeAsync_AForgedMessageHint_IsRefused()
    {
        // Arrange
        var request = Request();
        request.LtiMessageHint = "forged";

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task AuthorizeAsync_AnExpiredMessageHint_IsRefused()
    {
        // Arrange
        var request = Request();

        _clock.UtcNow = _now.AddMinutes(6);

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_AHintMadeForSomeoneElse_IsRefused()
    {
        // Act: the hint is user-1's, but user-2 is signed in.
        var result = await CreateService().AuthorizeAsync(Request(), "user-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("login_required", result.Error);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task AuthorizeAsync_ALoginHintThatIsNotThePersons_IsRefused()
    {
        // Arrange
        var request = Request();
        request.LoginHint = "someone-else";

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_AHintMadeForAnotherTool_IsRefused()
    {
        // Arrange
        var request = Request(toolId: "tool-2");

        // Act
        var result = await CreateService().AuthorizeAsync(request, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_AReplayedNonce_IsRefused()
    {
        // Arrange
        var service = CreateService();
        var first = await service.AuthorizeAsync(Request(), "user-1", TestContext.Current.CancellationToken);

        // Act: a new hint, the same nonce.
        var replayed = await CreateService().AuthorizeAsync(Request(), "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded, first.ErrorDescription);
        Assert.Equal("invalid_request", replayed.Error);
        Assert.Null(replayed.IdToken);
    }

    [Fact]
    public async Task AuthorizeAsync_ADisabledTool_IsRefused()
    {
        // Arrange
        _tool.Enabled = false;

        // Act
        var result = await CreateService().AuthorizeAsync(Request(), "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("unauthorized_client", result.Error);
        Assert.Null(result.RedirectUri);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenTheItemNoLongerLaunchesTheTool_IsRefused()
    {
        // Act
        var result = await CreateService(contextIsGone: true).AuthorizeAsync(Request(), "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invalid_request", result.Error);
        Assert.Null(result.IdToken);
    }

    private LtiAuthorizationRequest Request(string toolId = "tool-1")
        => new()
        {
            Scope = "openid",
            ResponseType = "id_token",
            ResponseMode = "form_post",
            Prompt = "none",
            ClientId = "client-1",
            RedirectUri = RedirectUri,
            LoginHint = LtiSubjects.For(_tool, "user-1"),
            LtiMessageHint = new LtiMessageHints(_protection, _clock).Create(new LtiMessageHint
            {
                MessageType = LtiConstants.MessageTypes.ResourceLinkRequest,
                ToolId = toolId,
                UserId = "user-1",
                CourseId = "course-1",
                ItemId = "item-1",
                EnrollmentId = "enrollment-1",
                Roles = [LtiConstants.Roles.Learner],
            }),
            State = "state-1",
            Nonce = "nonce-1",
        };

    private LtiAuthorizationService CreateService(bool contextIsGone = false)
        => new(
            new FakeToolDirectory(_tool),
            new LtiMessageHints(_protection, _clock),
            new LtiNonceStore(_cache, new KeyedLock(), _clock),
            new FakeLaunchContextFactory(contextIsGone),
            new FakePlatform(),
            CreateKeys(),
            _clock,
            NullLogger<LtiAuthorizationService>.Instance);

    private LtiPlatformKeys CreateKeys()
    {
        var options = new SigningKeyOptions();

        LtiPlatformKeys.Configure(options.GetOrAdd(LtiConstants.PlatformKeySet));

        return new LtiPlatformKeys(new SigningKeyRing(_keyDocuments, _secrets, new RecordingLock(), Options.Create(options), [], _clock, NullLogger<SigningKeyRing>.Instance));
    }

    private sealed class FakeToolDirectory : ILtiToolDirectory
    {
        private readonly ExternalTool _tool;

        public FakeToolDirectory(ExternalTool tool)
        {
            _tool = tool;
        }

        public Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(clientId == _tool.ClientId ? _tool : null);

        public Task<ExternalTool> FindByIdAsync(string toolId, CancellationToken cancellationToken = default)
            => Task.FromResult(toolId == _tool.ItemId ? _tool : null);

        public Task<bool> IsRedirectUriAllowedAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default)
            => Task.FromResult(clientId == _tool.ClientId && redirectUri == RedirectUri);

        public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(ExternalTool tool, bool refresh = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SecurityKey>>([]);
    }

    private sealed class FakeLaunchContextFactory : ILtiLaunchContextFactory
    {
        private readonly bool _gone;

        public FakeLaunchContextFactory(bool gone)
        {
            _gone = gone;
        }

        public Task<LtiLaunchContext> CreateAsync(LtiMessageHint hint, ExternalTool tool, CancellationToken cancellationToken = default)
            => Task.FromResult(_gone ? null : new LtiLaunchContext
            {
                MessageType = hint.MessageType,
                UserId = hint.UserId,
                Subject = LtiSubjects.For(tool, hint.UserId),
                Roles = hint.Roles,
                CourseId = hint.CourseId,
                CourseTitle = "Forklift Safety",
                ItemId = hint.ItemId,
                ItemTitle = "Simulator",
                TargetLinkUri = tool.TargetLinkUri,
                Locale = "en-US",
            });
    }

    private sealed class FakePlatform : ILtiPlatform
    {
        public Task<LtiPlatformInfo> GetAsync()
            => Task.FromResult(new LtiPlatformInfo
            {
                Issuer = "https://lms.example.com",
                Guid = "guid-1",
                Name = "Example LMS",
                DeepLinkingReturnUrl = "https://lms.example.com/learning/lti/deep-linking/return",
            });
    }
}
