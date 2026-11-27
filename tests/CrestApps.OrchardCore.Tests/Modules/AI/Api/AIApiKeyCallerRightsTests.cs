using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// Round 3: what an AI API key may do. A server key with a role other than Administrator got none of that role's
/// permissions, since Orchard Core grants a role's permissions through permission claims, not role claims. A browser key
/// is public, yet the AI tools treated its caller as signed in, with the Authenticated role's rights.
/// </summary>
public sealed class AIApiKeyCallerRightsTests
{
    private static readonly DateTime _now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AServerKeyWithARole_IsSignedInWithThatRolesPermissions()
    {
        // Arrange
        var (key, prefix, hash) = AIApiKeys.Generate(AIApiKeyKind.Server);
        var stored = new AIApiKey { ItemId = "key-1", Prefix = prefix, Hash = hash, Kind = AIApiKeyKind.Server, Roles = ["Editor"], LastUsedUtc = _now };
        var roleService = new Mock<IRoleService>();
        roleService
            .Setup(service => service.GetRoleClaimsAsync("Editor", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Claim(Permission.ClaimType, "ManageAIProfiles")]);

        // Act
        var result = await AuthenticateAsync(stored, key, roleService.Object);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Principal.HasClaim(Permission.ClaimType, "ManageAIProfiles"));
        Assert.True(result.Principal.IsInRole("Editor"));
    }

    [Fact]
    public async Task ABrowserKey_GetsNoRolePermissions()
    {
        // Arrange
        var (key, prefix, hash) = AIApiKeys.Generate(AIApiKeyKind.Browser);
        var stored = new AIApiKey { ItemId = "key-2", Prefix = prefix, Hash = hash, Kind = AIApiKeyKind.Browser, Roles = ["Editor"], LastUsedUtc = _now };
        var roleService = new Mock<IRoleService>();

        // Act
        var result = await AuthenticateAsync(stored, key, roleService.Object);

        // Assert
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Principal.Claims, claim => claim.Type == Permission.ClaimType);
        roleService.Verify(service => service.GetRoleClaimsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABrowserKeysCaller_IsNotSignedInForAITools()
    {
        // Arrange
        var browserKey = AIApiKeys.CreatePrincipal(new AIApiKey { ItemId = "key-2", Kind = AIApiKeyKind.Browser, AllowedProfileIds = ["profile-1"] });
        using var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = browserKey } })
            .AddAllowingAuthorization()
            .BuildServiceProvider();

        // Act
        var allowed = await AIToolCallerAuthorization.IsAuthorizedAsync(services, new Permission("AnyToolPermission"));

        // Assert
        Assert.True(browserKey.Identity.IsAuthenticated);
        Assert.False(allowed);
    }

    [Fact]
    public async Task AServerKeysCaller_IsSignedInForAITools()
    {
        // Arrange
        var serverKey = AIApiKeys.CreatePrincipal(new AIApiKey { ItemId = "key-1", Kind = AIApiKeyKind.Server, Roles = ["Editor"] });
        using var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = serverKey } })
            .AddAllowingAuthorization()
            .BuildServiceProvider();

        // Act
        var allowed = await AIToolCallerAuthorization.IsAuthorizedAsync(services, new Permission("AnyToolPermission"));

        // Assert
        Assert.True(allowed);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(AIApiKey stored, string key, IRoleService roleService)
    {
        var store = new Mock<IAIApiKeyStore>();
        store.Setup(value => value.FindByPrefixAsync(stored.Prefix, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(roleService).BuildServiceProvider(),
        };
        httpContext.Request.Headers[AIApiKeys.HeaderName] = key;

        if (stored.Kind == AIApiKeyKind.Browser)
        {
            stored.AllowedOrigins = ["https://www.example.com"];
            httpContext.Request.Headers.Origin = "https://www.example.com";
        }

        var handler = new AIApiKeyAuthenticationHandler(
            new StaticOptionsMonitor(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            store.Object,
            clock.Object);

        await handler.InitializeAsync(new AuthenticationScheme(AIApiKeys.Scheme, null, typeof(AIApiKeyAuthenticationHandler)), httpContext);

        return await handler.AuthenticateAsync();
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public StaticOptionsMonitor(AuthenticationSchemeOptions value)
            => CurrentValue = value;

        public AuthenticationSchemeOptions CurrentValue { get; }

        public AuthenticationSchemeOptions Get(string name)
            => CurrentValue;

        public IDisposable OnChange(Action<AuthenticationSchemeOptions, string> listener)
            => null;
    }
}
