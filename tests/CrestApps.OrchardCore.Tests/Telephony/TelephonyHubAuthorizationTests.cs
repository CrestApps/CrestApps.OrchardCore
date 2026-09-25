using System.Security.Claims;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

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
                CallIds = ["call-1", "call-2"],
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
                CallIds = ["call-1", "call-2"],
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
    public void MergeRequest_GetCallIds_WithNullCallIds_ReturnsEmpty()
    {
        // Arrange: the canonical shape is the only shape, so a request that names no call merges nothing
        // rather than silently falling back to a second, duplicate representation of the same calls.
        var request = new MergeRequest
        {
            CallIds = null,
        };

        // Act
        var callIds = request.GetCallIds();

        // Assert
        Assert.Empty(callIds);
    }

    [Fact]
    public void MergeRequest_GetCallIds_DeduplicatesAndDropsBlankIdentifiers()
    {
        // Arrange
        var request = new MergeRequest
        {
            CallIds = ["call-1", "  ", "call-2", "call-1", null],
        };

        // Act
        var callIds = request.GetCallIds();

        // Assert
        Assert.Equal(["call-1", "call-2"], callIds);
    }

    // The transfer's own leg rings somebody else, so it is never in the caller's history; the call being transferred is.
    [Fact]
    public async Task GetConsult_OfTheCallersOwnCall_InvokesTheService()
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-1", CallId = "call-1" }]);
        harness.TelephonyService
            .Setup(value => value.GetConsultAsync(It.IsAny<ConsultTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        var result = await InvokeInShellAsync(harness, hub => hub.GetConsult(new ConsultTransferRequest { CallId = "call-1", ConsultCallId = "leg-9" }));

        // Assert
        Assert.True(result.Succeeded);
        harness.TelephonyService.Verify(
            value => value.GetConsultAsync(It.Is<ConsultTransferRequest>(request => request.ConsultCallId == "leg-9"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("GetConsult")]
    [InlineData("CompleteConsult")]
    [InlineData("CancelConsult")]
    public async Task ConsultCommands_OnSomebodyElsesCall_AreRefused(string command)
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-2", CallId = "call-1" }]);
        var request = new ConsultTransferRequest { CallId = "call-1", ConsultCallId = "leg-9" };

        // Act
        var result = await InvokeInShellAsync(harness, hub => command switch
        {
            "GetConsult" => hub.GetConsult(request),
            "CompleteConsult" => hub.CompleteConsult(request),
            _ => hub.CancelConsult(request),
        });

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0, harness.CommandExecutor.InvocationCount);
    }

    [Fact]
    public async Task Transfer_TellsTheProviderWhoIsTransferring_WhateverTheClientSent()
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-1", CallId = "call-1" }]);
        TransferRequest sent = null;
        harness.TelephonyService
            .Setup(value => value.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransferRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        await InvokeInShellAsync(harness, hub => hub.Transfer(new TransferRequest
        {
            CallId = "call-1",
            To = "+17025550199",
            Metadata = new Dictionary<string, string>
            {
                [TelephonyConstants.RequestMetadata.SoftPhoneUserId] = "user-2",
                [TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName] = "Somebody Else",
                [TelephonyConstants.RequestMetadata.SoftPhoneCredentialId] = "credential-1",
            },
        }));

        // Assert
        Assert.Equal("user-1", sent.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserId]);
        Assert.Equal("user-1", sent.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneUserDisplayName]);
        Assert.Equal("credential-1", sent.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneCredentialId]);
    }

    [Fact]
    public async Task WarmTransfer_RecordsItsConsultAsANewCallOfTheAgents()
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-1", CallId = "call-1" }]);
        harness.TelephonyService
            .Setup(value => value.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall
            {
                CallId = "consult-1",
                State = CallState.Connecting,
                Direction = CallDirection.Outbound,
                To = "2",
                Metadata = new Dictionary<string, object>
                {
                    [TelephonyConstants.CallMetadata.ConsultOf] = "call-1",
                    [TelephonyConstants.CallMetadata.ExtensionNumber] = "2",
                },
            }));

        // Act
        await InvokeInShellAsync(harness, hub => hub.Transfer(new TransferRequest { CallId = "call-1", To = "2", Mode = TransferMode.Warm }));

        // Assert
        var store = harness.ServiceProvider.GetRequiredService<ITelephonyInteractionStore>();
        var consult = await store.FindByCallIdAsync("user-1", "consult-1", TestContext.Current.CancellationToken);
        Assert.NotNull(consult);
        Assert.True(consult.IsExtension);
    }

    [Fact]
    public async Task BlindTransfer_RecordsNoNewCall()
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-1", CallId = "call-1" }]);
        harness.TelephonyService
            .Setup(value => value.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall
            {
                CallId = "call-1",
                State = CallState.OnHold,
                Metadata = new Dictionary<string, object> { [TelephonyConstants.CallMetadata.ConsultId] = "leg-9" },
            }));

        // Act
        await InvokeInShellAsync(harness, hub => hub.Transfer(new TransferRequest { CallId = "call-1", To = "2" }));

        // Assert
        var store = harness.ServiceProvider.GetRequiredService<ITelephonyInteractionStore>();
        Assert.Null(await store.FindByCallIdAsync("user-1", "leg-9", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Transfer_NamesTheConnectionItCameFrom_SoTheProviderCanRingThePhoneThatAsked()
    {
        // Arrange
        using var harness = CreateHarness("user-1", [new TelephonyInteraction { UserId = "user-1", CallId = "call-1" }]);
        TransferRequest sent = null;
        harness.TelephonyService
            .Setup(value => value.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransferRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(TelephonyResult.Success());

        // Act
        await InvokeInShellAsync(harness, hub => hub.Transfer(new TransferRequest
        {
            CallId = "call-1",
            To = "2",
            IsExtension = true,
            Mode = TransferMode.Warm,
            Metadata = new Dictionary<string, string> { [TelephonyConstants.RequestMetadata.SoftPhoneConnectionId] = "somebody-elses-connection" },
        }));

        // Assert
        Assert.Equal("connection-1", sent.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneConnectionId]);
    }

    // The transfer panel asks where a consult stands every couple of seconds; a line each at Information buried the log.
    [Theory]
    [InlineData("GetConsult", LogLevel.Debug)]
    [InlineData("CompleteConsult", LogLevel.Information)]
    [InlineData("CancelConsult", LogLevel.Information)]
    [InlineData("Transfer", LogLevel.Information)]
    public void HubActions_AreLoggedAtInformation_ExceptTheConsultPoll(string action, LogLevel expected)
    {
        Assert.Equal(expected, TelephonyHub.HubActionLogLevel(action));
    }

    private static HubAuthorizationHarness CreateHarness(
        string userId,
        IEnumerable<TelephonyInteraction> interactions = null,
        Action<IServiceCollection> configure = null)
    {
        var telephonyService = new Mock<ITelephonyService>();
        var targetPolicy = new Mock<ITransferTargetPolicy>();
        targetPolicy
            .Setup(value => value.ResolveAsync(It.IsAny<TransferRequest>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferRequest request, ClaimsPrincipal _, CancellationToken _) => TransferTargetDecision.Allow(request.To));
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
            .AddSingleton(targetPolicy.Object)
            .AddSingleton<IClock>(new StubClock(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton(shellHost.Object);

        configure?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();

        // The hub resolves scoped services through ShellScope.UsingChildScopeAsync, which requires an
        // ambient shell scope whose IShellHost can produce child scopes. Returning child scopes over the
        // same test service provider lets each hub invocation resolve the registered test doubles.
        var shellContext = new ShellContext
        {
            Settings = shellSettings,
            ServiceProvider = serviceProvider,
            IsActivated = true,
        };
        shellHost
            .Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>()))
            .ReturnsAsync(() => new ShellScope(shellContext));

        var hub = new TelephonyHub(
            NullLogger<TelephonyHub>.Instance,
            new PassThroughStringLocalizer<TelephonyHub>(),
            shellSettings,
            RedactorProviderFactory.Create())
        {
            Context = CreateHubCallerContext(userId),
        };

        return new HubAuthorizationHarness(
            hub,
            serviceProvider,
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

        public Task<TelephonyInteraction> UpdateByIdAsync(
            string interactionId,
            Func<TelephonyInteraction, bool> mutate,
            CancellationToken cancellationToken = default)
        {
            var interaction = _interactions.FirstOrDefault(value =>
                string.Equals(value.InteractionId, interactionId, StringComparison.Ordinal));

            if (interaction is not null)
            {
                mutate(interaction);
            }

            return Task.FromResult(interaction);
        }

        public Task<TelephonyInteraction> UpdateByProviderCallIdAsync(
            string providerName,
            string callId,
            Func<TelephonyInteraction, bool> mutate,
            CancellationToken cancellationToken = default)
        {
            var interaction = _interactions.FirstOrDefault(value =>
                string.Equals(value.ProviderName, providerName, StringComparison.Ordinal) &&
                string.Equals(value.CallId, callId, StringComparison.Ordinal));

            if (interaction is not null)
            {
                mutate(interaction);
            }

            return Task.FromResult(interaction);
        }

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

        public Task<TelephonyInteraction> FindByInteractionIdAsync(
            string userId,
            string interactionId,
            CancellationToken cancellationToken = default)
        {
            var interaction = _interactions.FirstOrDefault(value =>
                string.Equals(value.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(value.InteractionId, interactionId, StringComparison.Ordinal));

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

        public Task<IReadOnlyList<TelephonyInteraction>> GetActiveByUserAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(
            string providerName,
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<IReadOnlyList<TelephonyInteraction>> GetRecentAsync(
            string userId,
            int count,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TelephonyInteraction>>([]);

        public Task<int> GetUnreadVoicemailCountAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_interactions.Count(value =>
                value.UserId == userId && value.IsVoicemail && value.VoicemailReadUtc is null));

        public Task<TelephonyInteraction> MarkVoicemailReadAsync(
            string userId,
            string callId,
            DateTime readUtc,
            CancellationToken cancellationToken = default)
        {
            var interaction = _interactions.FirstOrDefault(value =>
                value.UserId == userId && value.CallId == callId);

            if (interaction is not null && interaction.IsVoicemail && interaction.VoicemailReadUtc is null)
            {
                interaction.VoicemailReadUtc = readUtc;
            }

            return Task.FromResult(interaction);
        }

        public Task<int> MarkAllVoicemailsReadAsync(
            string userId,
            DateTime readUtc,
            CancellationToken cancellationToken = default)
        {
            var count = 0;

            foreach (var interaction in _interactions.Where(value =>
                value.UserId == userId && value.IsVoicemail && value.VoicemailReadUtc is null))
            {
                interaction.VoicemailReadUtc = readUtc;
                count++;
            }

            return Task.FromResult(count);
        }
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
