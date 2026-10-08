using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The entry point editor was split into cards (General, Routing, Hours, Menu, Voicemail) that all post one form. These
/// pin what the driver stores from that post, confirmed live: a line keeps only the target and the mailbox its routing
/// kind can use, and the settings the other cards carry are kept as posted.
/// </summary>
public sealed class ContactCenterEntryPointDisplayDriverTests
{
    // A queue line that delivers to the queue's shared box has no agent inbox, so an agent picked for it before is cleared
    // rather than left looking as though they still receive the line's messages.
    [Fact]
    public async Task UpdateAsync_AQueueLineDeliveringToTheSharedBox_KeepsNoRecipientAgent()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "entry-1" };
        var posted = new EntryPointViewModel
        {
            Name = "  Main line ",
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = " queue-main ",
            TargetAgentId = "agent-1",
            VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox,
            VoicemailRecipientAgentId = "agent-2",
            ClosedAction = EntryPointClosedAction.Overflow,
            OverflowQueueId = "queue-overflow",
            Priority = InteractionPriority.High,
        };

        // Act
        await UpdateAsync(entryPoint, posted);

        // Assert
        Assert.Equal("Main line", entryPoint.Name);
        Assert.Equal(EntryPointTargetType.Queue, entryPoint.TargetType);
        Assert.Equal("queue-main", entryPoint.TargetQueueId);
        Assert.Null(entryPoint.TargetAgentId);
        Assert.Equal(EntryPointVoicemailDestination.QueueSharedBox, entryPoint.VoicemailDestination);
        Assert.Null(entryPoint.VoicemailRecipientAgentId);

        // The Hours and Routing cards post alongside the Voicemail card, and what they carry is kept.
        Assert.Equal(EntryPointClosedAction.Overflow, entryPoint.ClosedAction);
        Assert.Equal("queue-overflow", entryPoint.OverflowQueueId);
        Assert.Equal(InteractionPriority.High, entryPoint.Priority);
    }

    [Fact]
    public async Task UpdateAsync_AQueueLineDeliveringToAnAgentsInbox_KeepsThatAgent()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "entry-1" };
        var posted = new EntryPointViewModel
        {
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "queue-main",
            VoicemailDestination = EntryPointVoicemailDestination.AgentInbox,
            VoicemailRecipientAgentId = " agent-2 ",
        };

        // Act
        await UpdateAsync(entryPoint, posted);

        // Assert
        Assert.Equal(EntryPointVoicemailDestination.AgentInbox, entryPoint.VoicemailDestination);
        Assert.Equal("agent-2", entryPoint.VoicemailRecipientAgentId);
    }

    // A personal line's messages always go to its agent, so a shared box chosen on a queue line before it was switched
    // to an agent is not kept, and neither is the queue it used to ring.
    [Fact]
    public async Task UpdateAsync_AnAgentLine_DeliversToTheAgentsInbox_AndKeepsNoQueue()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "entry-1" };
        var posted = new EntryPointViewModel
        {
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = " agent-1 ",
            TargetQueueId = "queue-main",
            VoicemailDestination = EntryPointVoicemailDestination.QueueSharedBox,
            VoicemailRecipientAgentId = "agent-2",
            ClosedAction = EntryPointClosedAction.Voicemail,
            Priority = InteractionPriority.Lowest,
        };

        // Act
        await UpdateAsync(entryPoint, posted);

        // Assert
        Assert.Equal(EntryPointTargetType.Agent, entryPoint.TargetType);
        Assert.Equal("agent-1", entryPoint.TargetAgentId);
        Assert.Null(entryPoint.TargetQueueId);
        Assert.Equal(EntryPointVoicemailDestination.AgentInbox, entryPoint.VoicemailDestination);
        Assert.Null(entryPoint.VoicemailRecipientAgentId);
        Assert.Equal(EntryPointClosedAction.Voicemail, entryPoint.ClosedAction);
        Assert.Equal(InteractionPriority.Lowest, entryPoint.Priority);
    }

    // The shared cards and a call entry point's own cards come from two drivers that bind the same posted form.
    private static async Task UpdateAsync(ContactCenterEntryPoint entryPoint, EntryPointViewModel posted)
    {
        await CreateDriver().UpdateAsync(entryPoint, PostedFormUpdateModel.CreateContext(posted));
        await CreateVoiceDriver().UpdateAsync(entryPoint, PostedFormUpdateModel.CreateContext(posted));
    }

    private static ContactCenterEntryPointDisplayDriver CreateDriver()
    {
        var addresses = new Mock<IOmnichannelChannelEndpointManager>();
        addresses
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new(
            AdminFormOptionsProviderFactory.Create(),
            addresses.Object,
            Options.Create(new EntryPointChannelOptions()),
            [],
            Options.Create(new EntryPointAIAgentOptions()));
    }

    private static ContactCenterEntryPointVoiceDisplayDriver CreateVoiceDriver()
        => new(
            AdminFormOptionsProviderFactory.Create(),
            SiteServiceFactory.Create(new ContactCenterExternalTransferSettings()));
}
