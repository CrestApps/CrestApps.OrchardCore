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

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyHubAuthorizationTests
{
    private const string RequestedCallUnavailableMessage = "The requested call is not available.";

    [Fact]
    public async Task Hangup_OwnerCall_InvokesProviderOperation()
    {
        // Arrange
        using var harness = CreateHarness(
            "user-1",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
            ]);
        harness.TelephonyService
            .Setup(value => value.HangupAsync(
                It.Is<CallReference>(call => call.CallId == "call-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Hangup(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(1, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.HangupAsync(
                It.Is<CallReference>(call => call.CallId == "call-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Hangup_NonOwnerCall_ReturnsRedactedFailureWithoutInvokingProvider()
    {
        // Arrange
        using var harness = CreateHarness(
            "user-2",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
            ]);
        harness.TelephonyService
            .Setup(value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Hangup(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RequestedCallUnavailableMessage, result.Error);
        Assert.Equal(0, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Hangup_MissingCall_ReturnsSameRedactedFailureAsNonOwner()
    {
        // Arrange
        using var nonOwnerHarness = CreateHarness(
            "user-2",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
            ]);
        using var missingHarness = CreateHarness("user-2");

        // Act
        var nonOwnerResult = await InvokeInShellAsync(
            nonOwnerHarness,
            hub => hub.Hangup(new CallReference { CallId = "call-1" }));
        var missingResult = await InvokeInShellAsync(
            missingHarness,
            hub => hub.Hangup(new CallReference { CallId = "call-1" }));

        // Assert
        Assert.False(nonOwnerResult.Succeeded);
        Assert.False(missingResult.Succeeded);
        Assert.Equal(RequestedCallUnavailableMessage, nonOwnerResult.Error);
        Assert.Equal(nonOwnerResult.Error, missingResult.Error);
        Assert.Equal(0, nonOwnerHarness.CommandExecutor.InvocationCount);
        Assert.Equal(0, missingHarness.CommandExecutor.InvocationCount);
    }

    [Fact]
    public async Task Merge_OwnsOnlyOneCall_ReturnsRedactedFailureWithoutInvokingProvider()
    {
        // Arrange
        using var harness = CreateHarness(
            "user-1",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
            ]);
        harness.TelephonyService
            .Setup(value => value.MergeAsync(It.IsAny<MergeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Merge(new MergeRequest
            {
                PrimaryCallId = "call-1",
                SecondaryCallId = "call-2",
            }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RequestedCallUnavailableMessage, result.Error);
        Assert.Equal(0, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.MergeAsync(It.IsAny<MergeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Dial_NoExistingInteraction_InvokesProviderOperation()
    {
        // Arrange
        using var harness = CreateHarness("user-1");
        harness.TelephonyService
            .Setup(value => value.DialAsync(
                It.Is<DialRequest>(request => request.To == "+15551234567"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Dial(new DialRequest { To = "+15551234567" }));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(1, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.DialAsync(
                It.Is<DialRequest>(request => request.To == "+15551234567"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Merge_OwnsBothCalls_InvokesProviderOperation()
    {
        // Arrange
        using var harness = CreateHarness(
            "user-1",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-2",
                },
            ]);
        harness.TelephonyService
            .Setup(value => value.MergeAsync(It.IsAny<MergeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Merge(new MergeRequest
            {
                PrimaryCallId = "call-1",
                SecondaryCallId = "call-2",
            }));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(1, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.MergeAsync(It.IsAny<MergeRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Hangup_BlankCallId_ReturnsRedactedFailureWithoutInvokingProvider()
    {
        // Arrange
        using var harness = CreateHarness(
            "user-1",
            [
                new TelephonyInteraction
                {
                    UserId = "user-1",
                    CallId = "call-1",
                },
            ]);
        harness.TelephonyService
            .Setup(value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(
            harness,
            hub => hub.Hangup(new CallReference { CallId = "   " }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RequestedCallUnavailableMessage, result.Error);
        Assert.Equal(0, harness.CommandExecutor.InvocationCount);
        harness.TelephonyService.Verify(
            value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void MergeRequest_GetCallIds_WithNullCallIds_FallsBackToLegacyIdentifiers()
    {
        // Arrange
        var request = new MergeRequest
        {
            CallIds = null,
            PrimaryCallId = "call-1",
            SecondaryCallId = "call-2",
        };

        // Act
        var callIds = request.GetCallIds();

        // Assert
        Assert.Equal(["call-1", "call-2"], callIds);
    }

    private static HubAuthorizationHarness CreateHarness(
        string userId,
        IEnumerable<TelephonyInteraction> interactions = null)
    {
        var telephonyService = new Mock<ITelephonyService>();
        var commandExecutor = new PassThroughTelephonyCommandExecutor();
        var store = new InMemoryTelephonyInteractionStore(interactions);
        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };
        var shellHost = new Mock<IShellHost>();
        var services = new ServiceCollection()
            .AddSingleton<IAuthorizationService>(new AllowAuthorizationService())
            .AddSingleton<ITelephonyService>(telephonyService.Object)
            .AddSingleton<ITelephonyCommandExecutor>(commandExecutor)
            .AddSingleton<ITelephonyInteractionStore>(store)
            .AddSingleton(shellHost.Object)
            .BuildServiceProvider();

        // The hub resolves scoped services through ShellScope.UsingChildScopeAsync, which requires an
        // ambient shell scope whose IShellHost can produce child scopes. Returning child scopes over the
        // same test service provider lets each hub invocation resolve the registered test doubles.
        var shellContext = new ShellContext
        {
            Settings = shellSettings,
            ServiceProvider = services,
            IsActivated = true,
        };
        shellHost
            .Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>()))
            .ReturnsAsync(() => new ShellScope(shellContext));

        var hub = new TelephonyHub(
            NullLogger<TelephonyHub>.Instance,
            new PassThroughStringLocalizer<TelephonyHub>(),
            shellSettings)
        {
            Context = CreateHubCallerContext(userId),
        };

        return new HubAuthorizationHarness(
            hub,
            services,
            shellContext,
            telephonyService,
            commandExecutor);
    }

    private static async Task<TelephonyResult> InvokeInShellAsync(
        HubAuthorizationHarness harness,
        Func<TelephonyHub, Task<TelephonyResult>> action)
    {
        TelephonyResult result = null;

        await new ShellScope(harness.ShellContext).UsingServiceScopeAsync(async _ =>
        {
            result = await action(harness.Hub);
        });

        return result;
    }

    private static HubCallerContext CreateHubCallerContext(string userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
        ],
            "Test");
        var user = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext
        {
            User = user,
        };
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature(httpContext));
        var context = new Mock<HubCallerContext>();
        context.SetupGet(value => value.ConnectionId).Returns("connection-1");
        context.SetupGet(value => value.ConnectionAborted).Returns(TestContext.Current.CancellationToken);
        context.SetupGet(value => value.Features).Returns(features);
        context.SetupGet(value => value.User).Returns(user);
        context.SetupGet(value => value.UserIdentifier).Returns(userId);

        return context.Object;
    }

    private sealed class HubAuthorizationHarness : IDisposable
    {
        public HubAuthorizationHarness(
            TelephonyHub hub,
            ServiceProvider serviceProvider,
            ShellContext shellContext,
            Mock<ITelephonyService> telephonyService,
            PassThroughTelephonyCommandExecutor commandExecutor)
        {
            Hub = hub;
            ServiceProvider = serviceProvider;
            ShellContext = shellContext;
            TelephonyService = telephonyService;
            CommandExecutor = commandExecutor;
        }

        public TelephonyHub Hub { get; }

        public ServiceProvider ServiceProvider { get; }

        public ShellContext ShellContext { get; }

        public Mock<ITelephonyService> TelephonyService { get; }

        public PassThroughTelephonyCommandExecutor CommandExecutor { get; }

        public void Dispose()
        {
            ServiceProvider.Dispose();
        }
    }

    private sealed class PassThroughTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public int InvocationCount { get; private set; }

        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
        {
            InvocationCount++;

            return operation(CancellationToken.None);
        }
    }

    private sealed class InMemoryTelephonyInteractionStore : ITelephonyInteractionStore
    {
        private readonly List<TelephonyInteraction> _interactions;

        public InMemoryTelephonyInteractionStore(IEnumerable<TelephonyInteraction> interactions)
        {
            _interactions = interactions?.ToList() ?? [];
        }

        public Task CreateAsync(
            TelephonyInteraction interaction,
            CancellationToken cancellationToken = default)
        {
            _interactions.Add(interaction);

            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TelephonyInteraction interaction,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(
            TelephonyInteraction interaction,
            CancellationToken cancellationToken = default)
        {
            _interactions.Remove(interaction);

            return Task.CompletedTask;
        }

        public Task<TelephonyInteraction> FindByCallIdAsync(
            string userId,
            string callId,
            CancellationToken cancellationToken = default)
        {
            var interaction = _interactions.FirstOrDefault(value =>
                string.Equals(value.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(value.CallId, callId, StringComparison.Ordinal));

            return Task.FromResult(interaction);
        }

        public Task<TelephonyInteraction> FindByProviderCallIdAsync(
            string providerName,
            string callId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TelephonyInteraction>(null);

        public Task<TelephonyInteraction> FindActiveByUserAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TelephonyInteraction>(null);

        public Task<IReadOnlyList<TelephonyInteraction>> ListActiveByUserAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> ListActiveAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> ListActiveAsync(
            string providerName,
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> GetRecentAsync(
            string userId,
            int count,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);
    }

    private sealed class AllowAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; } = httpContext;
    }
}
