using System.Security.Claims;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// An agent with the soft phone open in several windows has a credential registered per window. Calls went to the one
/// registered last, so closing that window left the server dialing a credential nothing was listening on until another
/// window registered again. Each registration now remembers the window's connection, and a window that closes hands the
/// calls to one still open.
/// </summary>
public sealed class SoftPhoneConnectionRegistrationTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ACredentialRegisteredByAnOpenWindow_IsPreferred_OverOneRegisteredLaterByAWindowThatClosed()
    {
        // Arrange
        var stillOpen = new TelnyxAgentCredential
        {
            CredentialId = "window-a",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            RegisteredConnectionId = "connection-a",
            ExpiresUtc = _now.AddHours(1),
        };

        var closed = new TelnyxAgentCredential
        {
            CredentialId = "window-b",
            IssuedUtc = _now.AddSeconds(30),
            RegisteredUtc = _now.AddSeconds(35),
            RegisteredConnectionId = "connection-b",
            ConnectionClosedUtc = _now.AddSeconds(60),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([closed, stillOpen]);

        // Assert
        Assert.Equal("window-a", ordered[0].CredentialId);
    }

    [Fact]
    public void ACredentialWhoseWindowOnlyLostItsConnection_IsStillPreferred_OverOneNothingRegisteredOn()
    {
        // Arrange
        // The connection also closes when a window only loses its link to the server; its registration with the
        // provider carries on, and a credential minted but never registered on is no better a target.
        var reconnected = new TelnyxAgentCredential
        {
            CredentialId = "window-a",
            IssuedUtc = _now,
            RegisteredUtc = _now.AddSeconds(5),
            RegisteredConnectionId = "connection-a",
            ConnectionClosedUtc = _now.AddSeconds(60),
            ExpiresUtc = _now.AddHours(1),
        };

        var neverRegistered = new TelnyxAgentCredential
        {
            CredentialId = "window-b",
            IssuedUtc = _now.AddSeconds(90),
            ExpiresUtc = _now.AddHours(1),
        };

        // Act
        var ordered = TelnyxAgentCredentialSelection.OrderByDeliveryPreference([neverRegistered, reconnected]);

        // Assert
        Assert.Equal("window-a", ordered[0].CredentialId);
    }

    [Fact]
    public async Task TheTelnyxRegistrar_RecordsTheConnection_AndItsClosing()
    {
        // Arrange
        var store = new Mock<ITelnyxAgentCredentialStore>();
        store
            .Setup(value => value.MarkRegisteredAsync("user-1", "credential-1", "connection-1", _now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var registrar = new TelnyxSoftPhoneCredentialRegistrar(store.Object, new StubClock(_now));

        // Act
        var connectionRegistrar = Assert.IsAssignableFrom<ISoftPhoneConnectionRegistrar>(registrar);
        var registered = await connectionRegistrar.ReportRegisteredAsync("user-1", "credential-1", "connection-1", TestContext.Current.CancellationToken);
        await connectionRegistrar.ReportConnectionClosedAsync("user-1", "connection-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(registered);
        store.Verify(value => value.MarkConnectionClosedAsync("user-1", "connection-1", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportCredentialRegistered_TellsTheRegistrarWhichConnectionRegistered()
    {
        // Arrange
        using var harness = new Harness();

        // Act
        await harness.InvokeAsync(hub => hub.ReportCredentialRegistered("credential-1"));

        // Assert
        harness.ConnectionRegistrar.Verify(
            value => value.ReportRegisteredAsync("user-1", "credential-1", "connection-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ARegistrarThatCannotTrackConnections_IsStillToldOfTheRegistration()
    {
        // Arrange
        using var harness = new Harness(tracksConnections: false);

        // Act
        await harness.InvokeAsync(hub => hub.ReportCredentialRegistered("credential-1"));

        // Assert
        harness.Registrar.Verify(
            value => value.ReportRegisteredAsync("user-1", "credential-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AClosingConnection_IsReportedToTheRegistrar()
    {
        // Arrange
        using var harness = new Harness();

        // Act
        await harness.InvokeAsync(hub => hub.OnDisconnectedAsync(null));

        // Assert
        harness.ConnectionRegistrar.Verify(
            value => value.ReportConnectionClosedAsync("user-1", "connection-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ARegistrarThatFailsOnAClosingConnection_DoesNotFailTheDisconnect()
    {
        // Arrange
        using var harness = new Harness();
        harness.ConnectionRegistrar
            .Setup(value => value.ReportConnectionClosedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        // Act
        var exception = await Record.ExceptionAsync(() => harness.InvokeAsync(hub => hub.OnDisconnectedAsync(null)));

        // Assert
        Assert.Null(exception);
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly ShellContext _shellContext;
        private readonly TelephonyHub _hub;

        public Harness(bool tracksConnections = true)
        {
            Registrar.SetupGet(value => value.ProviderName).Returns("ProviderA");
            ConnectionRegistrar = tracksConnections ? Registrar.As<ISoftPhoneConnectionRegistrar>() : new Mock<ISoftPhoneConnectionRegistrar>();

            var shellSettings = new ShellSettings { Name = "TenantA" };
            var shellHost = new Mock<IShellHost>();

            _services = new ServiceCollection()
                .AddSingleton<IAuthorizationService>(new AllowAuthorizationService())
                .AddSingleton(Registrar.Object)
                .AddSingleton<IClock>(new StubClock(_now))
                .AddSingleton(shellHost.Object)
                .BuildServiceProvider();

            _shellContext = new ShellContext
            {
                Settings = shellSettings,
                ServiceProvider = _services,
                IsActivated = true,
            };
            shellHost
                .Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>()))
                .ReturnsAsync(() => new ShellScope(_shellContext));

            _hub = new TelephonyHub(
                NullLogger<TelephonyHub>.Instance,
                new PassThroughStringLocalizer<TelephonyHub>(),
                shellSettings,
                RedactorProviderFactory.Create())
            {
                Context = CreateHubCallerContext("user-1"),
            };
        }

        public Mock<ISoftPhoneCredentialRegistrar> Registrar { get; } = new();

        public Mock<ISoftPhoneConnectionRegistrar> ConnectionRegistrar { get; }

        public Task InvokeAsync(Func<TelephonyHub, Task> action)
            => new ShellScope(_shellContext).UsingServiceScopeAsync(_ => action(_hub));

        public void Dispose()
            => _services.Dispose();

        private static HubCallerContext CreateHubCallerContext(string userId)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
            ],
                "Test");
            var user = new ClaimsPrincipal(identity);
            var features = new FeatureCollection();
            features.Set<IHttpContextFeature>(new TestHttpContextFeature(new DefaultHttpContext { User = user }));

            var context = new Mock<HubCallerContext>();
            context.SetupGet(value => value.ConnectionId).Returns("connection-1");
            context.SetupGet(value => value.ConnectionAborted).Returns(TestContext.Current.CancellationToken);
            context.SetupGet(value => value.Features).Returns(features);
            context.SetupGet(value => value.User).Returns(user);
            context.SetupGet(value => value.UserIdentifier).Returns(userId);

            return context.Object;
        }
    }

    private sealed class AllowAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; } = httpContext;
    }
}
