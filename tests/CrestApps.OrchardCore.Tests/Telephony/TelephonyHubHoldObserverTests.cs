using System.Security.Claims;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
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
/// A provider that holds a call in the agent's browser accepts the command and never reports the hold, so the hub is
/// the one place the server learns of it. These pin that every observer hears of an accepted hold and resume, dated by
/// when the agent asked and tied to the call the agent's own history names, and of nothing the provider refused.
/// </summary>
public sealed class TelephonyHubHoldObserverTests
{
    private static readonly DateTime _nowUtc = new DateTime(2026, 9, 23, 21, 10, 3, DateTimeKind.Utc).AddTicks(4567891);

    [Fact]
    public async Task Hold_TheProviderAccepted_TellsTheObserverWhenTheAgentAsked()
    {
        // Arrange
        using var harness = new Harness();
        harness.TelephonyService
            .Setup(value => value.HoldAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1", State = CallState.OnHold, IsOnHold = true }));

        // Act
        var result = await harness.InvokeAsync(hub => hub.Hold(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.True(result.Succeeded);
        var change = Assert.Single(harness.Changes);
        Assert.True(change.IsOnHold);
        Assert.Equal("call-1", change.CallId);
        Assert.Equal("user-1", change.UserId);
        Assert.Equal("interaction-1", change.InteractionId);
        Assert.Equal("ProviderA", change.ProviderName);
        Assert.Equal(_nowUtc, change.ChangedUtc);
    }

    [Fact]
    public async Task Resume_TheProviderAccepted_TellsTheObserverTheCallIsOffHold()
    {
        // Arrange
        using var harness = new Harness();
        harness.TelephonyService
            .Setup(value => value.ResumeAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1", State = CallState.Connected }));

        // Act
        await harness.InvokeAsync(hub => hub.Resume(new CallReference { CallId = "call-1" }));

        // Assert
        var change = Assert.Single(harness.Changes);
        Assert.False(change.IsOnHold);
        Assert.Equal(_nowUtc, change.ChangedUtc);
    }

    [Fact]
    public async Task Hold_TheProviderRefused_TellsNobody()
    {
        // Arrange
        using var harness = new Harness();
        harness.TelephonyService
            .Setup(value => value.HoldAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Failed("no"));

        // Act
        var result = await harness.InvokeAsync(hub => hub.Hold(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(harness.Changes);
    }

    [Fact]
    public async Task Hold_OnACallThatIsNotTheAgents_TellsNobody()
    {
        // Arrange
        using var harness = new Harness();

        // Act
        var result = await harness.InvokeAsync(hub => hub.Hold(new CallReference { CallId = "someone-elses-call" }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(harness.Changes);
        harness.TelephonyService.Verify(
            value => value.HoldAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Hold_AnObserverThatFails_DoesNotFailTheAgentsCommand()
    {
        // Arrange
        using var harness = new Harness(failingObserver: true);
        harness.TelephonyService
            .Setup(value => value.HoldAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = "call-1", State = CallState.OnHold, IsOnHold = true }));

        // Act
        var result = await harness.InvokeAsync(hub => hub.Hold(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Single(harness.Changes);
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly ShellContext _shellContext;
        private readonly TelephonyHub _hub;

        public Harness(bool failingObserver = false)
        {
            var interaction = new TelephonyInteraction
            {
                InteractionId = "interaction-1",
                CallId = "call-1",
                ProviderName = "ProviderA",
                UserId = "user-1",
            };

            var store = new Mock<ITelephonyInteractionStore>();
            store
                .Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var observer = new Mock<ITelephonyCallHoldObserver>();
            var setup = observer
                .Setup(value => value.CallHoldChangedAsync(It.IsAny<TelephonyCallHoldChange>(), It.IsAny<CancellationToken>()))
                .Callback<TelephonyCallHoldChange, CancellationToken>((change, _) => Changes.Add(change));

            if (failingObserver)
            {
                setup.ThrowsAsync(new InvalidOperationException("recording failed"));
            }
            else
            {
                setup.Returns(Task.CompletedTask);
            }

            var shellSettings = new ShellSettings { Name = "TenantA" };
            var shellHost = new Mock<IShellHost>();

            _services = new ServiceCollection()
                .AddSingleton<IAuthorizationService>(new AllowAuthorizationService())
                .AddSingleton(TelephonyService.Object)
                .AddSingleton<ITelephonyCommandExecutor>(new PassThroughTelephonyCommandExecutor())
                .AddSingleton(store.Object)
                .AddSingleton(observer.Object)
                .AddSingleton<IClock>(new StubClock(_nowUtc))
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

        public Mock<ITelephonyService> TelephonyService { get; } = new();

        public List<TelephonyCallHoldChange> Changes { get; } = [];

        public async Task<TelephonyResult> InvokeAsync(Func<TelephonyHub, Task<TelephonyResult>> action)
        {
            TelephonyResult result = null;

            await new ShellScope(_shellContext).UsingServiceScopeAsync(async _ =>
            {
                result = await action(_hub);
            });

            return result;
        }

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

    private sealed class PassThroughTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
            => operation(CancellationToken.None);
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
