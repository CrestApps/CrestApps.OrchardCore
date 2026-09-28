using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class VoicemailDeliveryTests
{
    [Fact]
    public void AnEntryPointSavedBeforeTheChoiceExisted_StillDeliversToItsAgentInbox()
    {
        // Arrange
        // An entry point stored before the destination could be chosen has no destination at all, which reads back as
        // the agent inbox: nothing changes for a tenant that has not opted in.
        var entryPoint = QueueLine();
        entryPoint.VoicemailRecipientAgentId = "agent-inbox";
        var interaction = new Interaction { QueueId = "queue-main" };

        // Act
        var changed = VoicemailDelivery.StampMailbox(interaction, entryPoint, "queue-main");

        // Assert
        Assert.True(changed);
        Assert.Equal(EntryPointVoicemailDestination.AgentInbox, new ContactCenterEntryPoint().VoicemailDestination);
        Assert.Equal("agent-inbox", interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.MailboxAgentMetadataKey]);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey));
        Assert.Equal(VoicemailRecipient.ForAgent("agent-inbox"), VoicemailDelivery.Resolve(interaction));
    }

    [Fact]
    public void AQueueLineDeliveringToTheSharedBox_RecordsTheQueueNotAnAgent()
    {
        // Arrange
        var entryPoint = QueueLine();
        entryPoint.VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox;
        entryPoint.VoicemailRecipientAgentId = "agent-inbox";
        var interaction = new Interaction { QueueId = "queue-main" };

        // Act
        VoicemailDelivery.StampMailbox(interaction, entryPoint, "queue-main");

        // Assert
        Assert.Equal("queue-main", interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey]);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.MailboxAgentMetadataKey));
        Assert.Equal(VoicemailRecipient.ForSharedQueue("queue-main"), VoicemailDelivery.Resolve(interaction));
    }

    [Fact]
    public void ASharedBoxMessage_GoesToTheQueueTheCallerWasWaitingIn()
    {
        // Arrange
        // The menu sent the caller to another team's queue, or the line's queue overflowed: that team took the call,
        // so that team hears the message.
        var entryPoint = QueueLine();
        entryPoint.VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox;
        var interaction = new Interaction();
        VoicemailDelivery.StampMailbox(interaction, entryPoint);
        interaction.QueueId = "queue-billing";

        // Act
        var recipient = VoicemailDelivery.Resolve(interaction);

        // Assert
        Assert.Equal(VoicemailRecipient.ForSharedQueue("queue-billing"), recipient);
    }

    [Fact]
    public void AnAgentWhoLetTheOfferRingOut_StillGetsTheMessage()
    {
        // Arrange
        var entryPoint = QueueLine();
        entryPoint.VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox;
        var interaction = new Interaction { QueueId = "queue-main" };
        VoicemailDelivery.StampMailbox(interaction, entryPoint, "queue-main");

        // Act
        var recipient = VoicemailDelivery.Resolve(interaction, offeredAgentId: "agent-rang");

        // Assert
        Assert.Equal(VoicemailRecipient.ForAgent("agent-rang"), recipient);
    }

    [Fact]
    public void APersonalLine_NeverRecordsAMailbox_AndItsMessagesGoToItsAgent()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent-owner",
            VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox,
        };
        var interaction = new Interaction();
        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = "agent-owner";

        // Act
        var changed = VoicemailDelivery.StampMailbox(interaction, entryPoint);

        // Assert
        Assert.False(changed);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.SharedMailboxQueueMetadataKey));
        Assert.Equal(VoicemailRecipient.ForAgent("agent-owner"), VoicemailDelivery.Resolve(interaction));
    }

    [Fact]
    public void TheMailboxACallArrivedWith_IsNotReplacedLater()
    {
        // Arrange
        var entryPoint = QueueLine();
        entryPoint.VoicemailRecipientAgentId = "agent-inbox";
        var interaction = new Interaction();
        VoicemailDelivery.StampMailbox(interaction, entryPoint, "queue-main");
        entryPoint.VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox;

        // Act
        var changed = VoicemailDelivery.StampMailbox(interaction, entryPoint, "queue-main");

        // Assert
        Assert.False(changed);
        Assert.Equal(VoicemailRecipient.ForAgent("agent-inbox"), VoicemailDelivery.Resolve(interaction));
    }

    [Fact]
    public void ALineWithNoMailbox_DeliversToNobody()
    {
        // Arrange
        var interaction = new Interaction { QueueId = "queue-main" };

        // Act
        var changed = VoicemailDelivery.StampMailbox(interaction, QueueLine(), "queue-main");

        // Assert
        Assert.False(changed);
        Assert.Equal(VoicemailRecipient.None, VoicemailDelivery.Resolve(interaction));
    }

    private static ContactCenterEntryPoint QueueLine()
        => new()
        {
            ItemId = "entry-1",
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "queue-main",
        };
}
