using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The tenant's recording disclosure: which calls it is given on, and what giving it records. Giving it is the only
/// way consent is captured, so a tenant that will not record until consent is captured depends on it to record at all.
/// </summary>
public sealed class RecordingDisclosureServiceTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";

    private static readonly DateTime _now = new(2026, 10, 8, 14, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RecordingDisclosureCallType.Inbound)]
    [InlineData(RecordingDisclosureCallType.AIVoiceAgent)]
    [InlineData(RecordingDisclosureCallType.Agent)]
    public void TheDisclosure_IsGivenOnEveryTypeOfCallByDefault_OnceTurnedOn(RecordingDisclosureCallType callType)
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = true,
            RecordingDisclosureText = "  " + Disclosure + "  ",
        };

        // Act
        var disclosure = RecordingDisclosureService.GetDisclosure(settings, callType);

        // Assert
        Assert.Equal(Disclosure, disclosure);
    }

    [Fact]
    public void TheDisclosure_IsOffByDefault()
    {
        // Arrange
        // Existing tenants are not suddenly told something new on every call by an upgrade.
        var settings = new ContactCenterRecordingSettings
        {
            RecordingDisclosureText = Disclosure,
        };

        // Act
        var disclosure = RecordingDisclosureService.GetDisclosure(settings, RecordingDisclosureCallType.Inbound);

        // Assert
        Assert.Null(disclosure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void AnEmptyDisclosure_IsNotGiven(string text)
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = true,
            RecordingDisclosureText = text,
        };

        // Act
        var disclosure = RecordingDisclosureService.GetDisclosure(settings, RecordingDisclosureCallType.Agent);

        // Assert
        Assert.Null(disclosure);
    }

    [Theory]
    [InlineData(RecordingDisclosureCallType.Inbound)]
    [InlineData(RecordingDisclosureCallType.AIVoiceAgent)]
    [InlineData(RecordingDisclosureCallType.Agent)]
    public void ATypeOfCallTurnedOff_IsNotGivenTheDisclosure(RecordingDisclosureCallType callType)
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = true,
            RecordingDisclosureText = Disclosure,
            DiscloseOnInboundCalls = callType != RecordingDisclosureCallType.Inbound,
            DiscloseOnAIVoiceCalls = callType != RecordingDisclosureCallType.AIVoiceAgent,
            PromptAgentsToDisclose = callType != RecordingDisclosureCallType.Agent,
        };

        // Act
        var disclosure = RecordingDisclosureService.GetDisclosure(settings, callType);

        // Assert
        Assert.Null(disclosure);
    }

    [Fact]
    public async Task RecordingTheDisclosure_StampsIt_CapturesConsent_AndPublishesTheWords()
    {
        // Arrange
        var harness = new Harness(new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = true,
            RecordingDisclosureText = Disclosure,
        });

        // Act
        var recorded = await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(recorded);
        Assert.Equal(_now, harness.Interaction.RecordingDisclosedUtc);
        Assert.Equal(_now, harness.Interaction.RecordingConsentCapturedUtc);
        harness.Interactions.Verify(manager => manager.UpdateAsync(harness.Interaction, null, It.IsAny<CancellationToken>()), Times.Once);

        var published = Assert.Single(harness.Published);
        Assert.Equal(ContactCenterConstants.Events.RecordingDisclosed, published.EventType);
        Assert.Equal("agent-7", published.ActorId);
        var data = published.GetData<RecordingDisclosedEventData>();
        Assert.Equal(ContactCenterConstants.RecordingDisclosureMethod.Agent, data.Method);
        Assert.Equal(Disclosure, data.Text);
    }

    [Fact]
    public async Task ACallerAlreadyTold_IsNotRecordedAgain()
    {
        // Arrange
        var harness = new Harness(new ContactCenterRecordingSettings());
        var told = _now.AddMinutes(-2);
        harness.Interaction.RecordingDisclosedUtc = told;

        // Act
        var recorded = await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(recorded);
        Assert.Equal(told, harness.Interaction.RecordingDisclosedUtc);
        Assert.Empty(harness.Published);
    }

    [Fact]
    public async Task ConsentCapturedEarlier_IsKept()
    {
        // Arrange
        var harness = new Harness(new ContactCenterRecordingSettings());
        var consented = _now.AddMinutes(-5);
        harness.Interaction.RecordingConsentCapturedUtc = consented;

        // Act
        await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Announcement, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(consented, harness.Interaction.RecordingConsentCapturedUtc);
        Assert.Null(Assert.Single(harness.Published).ActorId);
    }

    [Fact]
    public async Task ATenantThatWaitsForConsent_StartsTheRecordingOnceTheCallerIsTold()
    {
        // Arrange
        // The recording was refused when the call connected, because the caller had not been told yet.
        var harness = new Harness(new ContactCenterRecordingSettings
        {
            RecordAllCalls = true,
            ConsentModel = RecordingConsentModel.AllParties,
            RequireExplicitConsent = true,
        });

        // Act
        await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Announcement, TestContext.Current.CancellationToken);

        // Assert
        harness.Recording.Verify(recording => recording.StartAsync("int-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false, RecordingConsentModel.AllParties)]
    [InlineData(true, true, RecordingConsentModel.SingleParty)]
    [InlineData(false, true, RecordingConsentModel.AllParties)]
    public async Task ATenantThatDoesNotWaitForConsent_DoesNotStartARecordingWhenTheCallerIsTold(bool recordAllCalls, bool requireConsent, RecordingConsentModel consentModel)
    {
        // Arrange
        // The recording either started when the call connected, or is started by a workflow or supervisor.
        var harness = new Harness(new ContactCenterRecordingSettings
        {
            RecordAllCalls = recordAllCalls,
            RequireExplicitConsent = requireConsent,
            ConsentModel = consentModel,
        });

        // Act
        await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Announcement, TestContext.Current.CancellationToken);

        // Assert
        harness.Recording.Verify(recording => recording.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACallAlreadyRecording_IsNotStartedAgain()
    {
        // Arrange
        var harness = new Harness(new ContactCenterRecordingSettings
        {
            RecordAllCalls = true,
            RequireExplicitConsent = true,
        });
        harness.Interaction.RecordingState = RecordingState.Paused;

        // Act
        await harness.Service.RecordDisclosedAsync("int-1", ContactCenterConstants.RecordingDisclosureMethod.Agent, TestContext.Current.CancellationToken);

        // Assert
        // Starting from any state but None would resume a secure pause the agent is relying on.
        harness.Recording.Verify(recording => recording.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Harness
    {
        public Harness(ContactCenterRecordingSettings settings)
        {
            Interactions
                .Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => Published.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            Recording
                .Setup(recording => recording.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(RecordingCommandResult.Success());

            var services = new ServiceCollection()
                .AddSingleton(publisher.Object)
                .AddSingleton(Recording.Object)
                .BuildServiceProvider();

            Service = new RecordingDisclosureService(
                services,
                Interactions.Object,
                SiteServiceFactory.Create(settings),
                new StubClock(_now),
                NullLogger<RecordingDisclosureService>.Instance);
        }

        public Interaction Interaction { get; } = new()
        {
            ItemId = "int-1",
            AgentId = "agent-7",
            Channel = InteractionChannel.Voice,
        };

        public Mock<IInteractionManager> Interactions { get; } = new();

        public Mock<IContactCenterRecordingService> Recording { get; } = new();

        public List<InteractionEvent> Published { get; } = [];

        public RecordingDisclosureService Service { get; }
    }
}
