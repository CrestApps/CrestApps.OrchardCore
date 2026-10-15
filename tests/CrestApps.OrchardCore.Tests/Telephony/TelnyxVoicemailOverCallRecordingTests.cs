using System.Net;
using System.Text;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// An automated voice agent's call is recorded from the answer. When the AI hands the caller to a queue and nobody
/// takes the call, it falls to voicemail, which records the message on the same leg. Telnyx stamps a command's
/// client_state on every later webhook of the call, so the voicemail's state used to label the conversation's
/// recording too, and the call was left without its recording.
/// </summary>
public sealed class TelnyxVoicemailOverCallRecordingTests
{
    [Fact]
    public async Task StartingAVoicemail_OnACallBeingRecorded_StopsTheCallRecordingFirst()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var starter = new TelnyxVoicemailRecordingStarter(
            CreateApiClient(handler),
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()),
            [CatalogWithRunningAiRecording().Object],
            NullLogger<TelnyxVoicemailRecordingStarter>.Instance);

        // Act
        var started = await starter.StartAsync("ctrl-1", "interaction-1", "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Collection(
            handler.Requests,
            stop => Assert.Equal("/v2/calls/ctrl-1/actions/record_stop", stop.Path),
            start => Assert.Equal("/v2/calls/ctrl-1/actions/record_start", start.Path));
    }

    [Fact]
    public async Task StartingAVoicemail_OnACallNotBeingRecorded_OnlyStartsTheVoicemail()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var starter = new TelnyxVoicemailRecordingStarter(
            CreateApiClient(handler),
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()),
            [new Mock<ICallRecordingCatalog>().Object],
            NullLogger<TelnyxVoicemailRecordingStarter>.Instance);

        // Act
        await starter.StartAsync("ctrl-1", "interaction-1", "user-1", TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v2/calls/ctrl-1/actions/record_start", request.Path);
    }

    [Fact]
    public async Task ATwoChannelRecordingLabelledAsTheVoicemail_IsFiledAsTheCallRecording()
    {
        // Arrange
        var catalog = CatalogWithRunningAiRecording();
        var (enqueuer, interaction, publisher) = CreateEnqueuer(catalog.Object);
        var saved = SavedRecording(TelnyxConstants.Recording.CallChannels);

        // Act
        var handled = await enqueuer.HandleAsync(saved, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        catalog.Verify(
            value => value.RegisterAsync(
                It.Is<CallRecordingRegistration>(registration =>
                    registration.Source == CallRecordingSource.AiAgent &&
                    registration.ProviderRecordingId == "rec-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.ProjectionMetadataKey));
        publisher.Verify(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheVoicemailsOwnSingleChannelRecording_IsStillFiledAsTheVoicemail()
    {
        // Arrange
        var catalog = CatalogWithRunningAiRecording();
        var (enqueuer, interaction, _) = CreateEnqueuer(catalog.Object);
        var saved = SavedRecording("single");

        // Act
        var handled = await enqueuer.HandleAsync(saved, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.True((bool)interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey]);
        catalog.Verify(
            value => value.RegisterAsync(It.IsAny<CallRecordingRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TelnyxCallEvent SavedRecording(string channels)
        => new()
        {
            EventType = "call.recording.saved",
            CallControlId = "ctrl-1",
            RecordingId = "rec-1",
            RecordingChannels = channels,
            ClientState = Encoding.UTF8.GetString(Convert.FromBase64String(
                TelnyxRecordingClientState.ForVoicemail("interaction-1", "user-1").ToClientState())),
        };

    private static Mock<ICallRecordingCatalog> CatalogWithRunningAiRecording()
    {
        var catalog = new Mock<ICallRecordingCatalog>();
        catalog
            .Setup(value => value.FindRunningAsync("ctrl-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallRecording
            {
                Source = CallRecordingSource.AiAgent,
                ProviderCallId = "ctrl-1",
                ActivityItemId = "activity-1",
            });
        catalog
            .Setup(value => value.RegisterAsync(It.IsAny<CallRecordingRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallRecording());

        return catalog;
    }

    private static (TelnyxRecordingIngestEnqueuer Enqueuer, Interaction Interaction, Mock<IContactCenterEventPublisher> Publisher) CreateEnqueuer(
        ICallRecordingCatalog catalog)
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "ctrl-1",
            TechnicalMetadata = new Dictionary<string, object>(),
        };

        var jobStore = new Mock<ITelnyxRecordingIngestJobStore>();
        jobStore
            .Setup(store => store.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc));

        var enqueuer = new TelnyxRecordingIngestEnqueuer(
            jobStore.Object,
            interactionManager.Object,
            [agentManager.Object],
            publisher.Object,
            Mock.Of<IContactCenterScopeExecutor>(),
            [catalog],
            clock.Object,
            NullLogger<TelnyxRecordingIngestEnqueuer>.Instance);

        return (enqueuer, interaction, publisher);
    }

    private static TelnyxOptions CreateOptions()
        => new()
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
        };

    private static TelnyxApiClient CreateApiClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        return new TelnyxApiClient(
            httpClient,
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
    }
}
