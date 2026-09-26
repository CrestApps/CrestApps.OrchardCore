using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class SharedVoicemailProjectionHandlerTests
{
    private static readonly DateTime _sentUtc = new(2026, 9, 26, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AMessageDeliveredToASharedBox_IsFiledUnderItsQueue_WithWhoCalled()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            CustomerAddress = "+15550001000",
        };
        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.SharedQueueMetadataKey] = "queue-main";

        var harness = new Harness(interaction);
        harness.Activities
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1", ContactContentItemId = "contact-1", ContactContentType = "Contact" });
        harness.Content
            .Setup(value => value.GetAsync("contact-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "contact-1", DisplayText = "Jane Doe" });

        // Act
        await harness.Handler.HandleAsync(SentToVoicemail(), TestContext.Current.CancellationToken);

        // Assert
        var filed = Assert.Single(harness.Filed);
        Assert.Equal("interaction-1", filed.InteractionId);
        Assert.Equal("queue-main", filed.QueueId);
        Assert.Equal("+15550001000", filed.CallerNumber);
        Assert.Equal("Jane Doe", filed.CallerName);
        Assert.Equal("contact-1", filed.ContactContentItemId);
        Assert.Equal("Contact", filed.ContactContentType);
        Assert.Equal(_sentUtc, filed.ReceivedUtc);
    }

    [Fact]
    public async Task AMessageForAnAgent_IsNotFiledInAnyBox()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "interaction-1" };
        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = "agent-1";
        var harness = new Harness(interaction);

        // Act
        await harness.Handler.HandleAsync(SentToVoicemail(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Filed);
    }

    [Fact]
    public async Task OtherEvents_AreIgnored()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "interaction-1" };
        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.SharedQueueMetadataKey] = "queue-main";
        var harness = new Harness(interaction);

        // Act
        await harness.Handler.HandleAsync(
            new InteractionEvent { EventType = ContactCenterConstants.Events.CallEnded, InteractionId = "interaction-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Filed);
    }

    private static InteractionEvent SentToVoicemail()
        => new()
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = "interaction-1",
            OccurredUtc = _sentUtc,
        };

    private sealed class Harness
    {
        public Harness(Interaction interaction)
        {
            var interactions = new Mock<IInteractionManager>();
            interactions
                .Setup(value => value.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

            var service = new Mock<ISharedVoicemailService>();
            service
                .Setup(value => value.DeliverAsync(It.IsAny<SharedVoicemail>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SharedVoicemail voicemail, CancellationToken _) =>
                {
                    Filed.Add(voicemail);

                    return voicemail;
                });

            var services = new ServiceCollection()
                .AddSingleton(Activities.Object)
                .AddSingleton(Content.Object)
                .AddSingleton(service.Object)
                .BuildServiceProvider();

            Handler = new SharedVoicemailProjectionHandler(interactions.Object, services);
        }

        public Mock<IOmnichannelActivityManager> Activities { get; } = new();

        public Mock<IContentManager> Content { get; } = new();

        public List<SharedVoicemail> Filed { get; } = [];

        public SharedVoicemailProjectionHandler Handler { get; }
    }
}
