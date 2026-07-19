using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceProviderRecordingTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _callerChannelId = "caller-1";
    private const string _bridgeId = AsteriskAriConstants.AgentBridgePrefix + "interaction-1-command-1";
    private const string _interactionId = "interaction-1";
    private static readonly string _recordingName = AsteriskAriConstants.RecordingNamePrefix + _interactionId;

    [Fact]
    public async Task SetRecordingStateAsync_WhenRecording_StartsRecordingOnResolvedConversationBridge()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal(_bridgeId, ariClient.StartedBridgeId);
        Assert.Equal(_recordingName, ariClient.StartedRecordingName);
        Assert.Equal(AsteriskAriConstants.RecordingFormat, ariClient.StartedFormat);
        Assert.Null(ariClient.UnpausedRecordingName);
        Assert.Equal(_recordingName, result.Metadata[ContactCenterConstants.RecordingMetadata.RecordingName]);
        Assert.Equal(_recordingName, result.Metadata[ContactCenterConstants.RecordingMetadata.StorageReference]);
        Assert.Equal(AsteriskAriConstants.RecordingFormat, result.Metadata[ContactCenterConstants.RecordingMetadata.Format]);
        Assert.Equal(
            AsteriskAriConstants.StoredRecordingRetrievalPathPrefix + _recordingName,
            result.Metadata[ContactCenterConstants.RecordingMetadata.RetrievalPath]);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenRecordingAndExistingRecordingIsPaused_ResumesIt()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StartResult = new AsteriskAriLiveRecording
            {
                Name = _recordingName,
                Format = AsteriskAriConstants.RecordingFormat,
                State = AsteriskAriConstants.RecordingPausedState,
            },
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_recordingName, ariClient.UnpausedRecordingName);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenPaused_PausesTheLiveRecording()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient();
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Paused),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_recordingName, ariClient.PausedRecordingName);
        Assert.Null(ariClient.StartedRecordingName);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenStopped_StopsAndReturnsStoredMetadata()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StopResult = new AsteriskAriStoredRecording
            {
                Name = _recordingName,
                Format = "ulaw",
                Duration = 42,
            },
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Stopped),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_recordingName, ariClient.StoppedRecordingName);
        Assert.Equal("ulaw", result.Metadata[ContactCenterConstants.RecordingMetadata.Format]);
        Assert.Equal("42", result.Metadata[ContactCenterConstants.RecordingMetadata.DurationSeconds]);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenStoppingAlreadyGoneRecording_IsIdempotentSuccess()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StopResult = null,
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Stopped),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(_recordingName, ariClient.StoppedRecordingName);
        Assert.Equal(_recordingName, result.Metadata[ContactCenterConstants.RecordingMetadata.RecordingName]);
        Assert.False(result.Metadata.ContainsKey(ContactCenterConstants.RecordingMetadata.DurationSeconds));
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenNoOwningBindingExists_FailsClosed()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient();
        var bindingStore = new TestRecordingBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("recording_call_not_owned", result.ErrorCode);
        Assert.Null(ariClient.StartedRecordingName);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenAriTimesOutAmbiguously_ReportsOutcomeUnknown()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StartException = new AsteriskAriException(
                nameof(IAsteriskAriClient.StartBridgeRecordingAsync),
                statusCode: null,
                "Asterisk ARI timed out before a response was observed.",
                new HttpRequestException("Asterisk ARI could not reach Asterisk.")),
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("recording_outcome_unknown", result.ErrorCode);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenProviderPreflightRejectsWithoutReachingAsterisk_ReportsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StartException = new AsteriskAriException(
                nameof(IAsteriskAriClient.StartBridgeRecordingAsync),
                statusCode: null,
                "The Asterisk provider is not configured."),
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("recording_failed", result.ErrorCode);
    }

    [Fact]
    public async Task SetRecordingStateAsync_WhenAriRejects_ReportsConfirmedFailure()
    {
        // Arrange
        var ariClient = new TestRecordingAriClient
        {
            StartException = new AsteriskAriException(
                nameof(IAsteriskAriClient.StartBridgeRecordingAsync),
                HttpStatusCode.BadRequest,
                "Asterisk ARI rejected the recording request."),
        };
        var bindingStore = CreateConnectedBindingStore();
        var service = CreateService(ariClient, bindingStore);

        // Act
        var result = await service.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("recording_failed", result.ErrorCode);
    }

    private static ContactCenterVoiceRecordingRequest CreateRequest(RecordingState state)
    {
        return new ContactCenterVoiceRecordingRequest
        {
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            State = state,
        };
    }

    private static TestRecordingBindingStore CreateConnectedBindingStore()
    {
        var store = new TestRecordingBindingStore();
        store.Seed(new AsteriskChannelTenantBinding
        {
            ChannelId = "crestapps-agent-interaction-1-command-1",
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            InteractionId = _interactionId,
            ProviderCallId = _callerChannelId,
            PeerChannelId = _callerChannelId,
            BridgeId = _bridgeId,
            State = AsteriskChannelBindingState.Connected,
            CreatedUtc = _now,
        });

        return store;
    }

    private static AsteriskContactCenterVoiceProvider CreateService(
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore bindingStore)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new AsteriskContactCenterVoiceProvider(
            Mock.Of<ITelephonyProviderResolver>(),
            new TestContactCenterFeatureWorkManager(),
            ariClient,
            bindingStore,
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
            clock.Object,
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            new TestStringLocalizer());
    }

    private sealed class TestStringLocalizer : IStringLocalizer<AsteriskContactCenterVoiceProvider>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }

    private sealed class TestRecordingAriClient : IAsteriskAriClient
    {
        public AsteriskAriLiveRecording StartResult { get; set; }

        public AsteriskAriStoredRecording StopResult { get; set; }

        public Exception StartException { get; set; }

        public string StartedBridgeId { get; private set; }

        public string StartedRecordingName { get; private set; }

        public string StartedFormat { get; private set; }

        public string PausedRecordingName { get; private set; }

        public string UnpausedRecordingName { get; private set; }

        public string StoppedRecordingName { get; private set; }

        public Task<AsteriskAriLiveRecording> StartBridgeRecordingAsync(string bridgeId, string recordingName, string format, CancellationToken cancellationToken)
        {
            if (StartException is not null)
            {
                return Task.FromException<AsteriskAriLiveRecording>(StartException);
            }

            StartedBridgeId = bridgeId;
            StartedRecordingName = recordingName;
            StartedFormat = format;

            return Task.FromResult(StartResult ?? new AsteriskAriLiveRecording
            {
                Name = recordingName,
                Format = format,
                State = "recording",
            });
        }

        public Task PauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            PausedRecordingName = recordingName;

            return Task.CompletedTask;
        }

        public Task UnpauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            UnpausedRecordingName = recordingName;

            return Task.CompletedTask;
        }

        public Task<AsteriskAriStoredRecording> StopBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken)
        {
            StoppedRecordingName = recordingName;

            return Task.FromResult(StopResult);
        }

        public Task<AsteriskAriChannel> SnoopChannelAsync(string channelId, string spy, string whisper, string snoopId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel { Id = snoopId });
        }

        public Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriChannel());
        }

        public Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AsteriskAriBridge { Id = bridgeId });
        }

        public Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AnswerAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HangupAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestRecordingBindingStore : IAsteriskChannelTenantBindingStore
    {
        private readonly List<AsteriskChannelTenantBinding> _bindings = [];

        public void Seed(AsteriskChannelTenantBinding binding)
        {
            _bindings.Add(binding);
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(_bindings.ToArray());
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_bindings.Count > 0);
        }

        public Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
        {
            return Task.FromResult(_bindings.Find(binding => binding.ChannelId == channelId));
        }

        public Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
        {
            return Task.FromResult<IReadOnlyCollection<AsteriskChannelTenantBinding>>(
                _bindings.Where(binding => binding.PeerChannelId == peerChannelId).ToArray());
        }

        public Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
        {
            _bindings.Add(binding);

            return Task.FromResult(true);
        }

        public Task RemoveByChannelIdAsync(string channelId)
        {
            _bindings.RemoveAll(binding => binding.ChannelId == channelId);

            return Task.CompletedTask;
        }

        public Task<bool> MarkConnectedAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TryPromoteOfferingAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> MarkCallerDetachedAsync(string channelId)
        {
            return Task.FromResult(false);
        }

        public Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
        {
            return Task.FromResult<AsteriskChannelTeardownClaim>(null);
        }
    }
}
