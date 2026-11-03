using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Which call a tool on a live session is acting for.
/// </summary>
/// <remarks>
/// A tool that looks the caller up, or records what they asked for, could otherwise only go by what the caller said,
/// and anybody can say a name or read out a number.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task TheSessionsTools_SeeWhichCallTheyAreActingFor_FromTheCallsActivity()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Activity.PreferredDestination = "+19305550100";
        harness.Activity.ChannelEndpointId = "endpoint-1";
        harness.Activity.ContactContentItemId = "contact-1";
        harness.Activity.ContactContentType = "Person";

        // Act
        await harness.RunAsync();

        // Assert
        var items = harness.Orchestrator.InvocationScopeAtStart.Items;
        Assert.Equal("activity-1", items[VoiceCallInvocationItems.ActivityId]);
        Assert.Equal("+19305550100", items[VoiceCallInvocationItems.CustomerNumber]);
        Assert.Equal("endpoint-1", items[VoiceCallInvocationItems.ChannelEndpointId]);
        Assert.Equal("contact-1", items[VoiceCallInvocationItems.ContactContentItemId]);
        Assert.Equal("Person", items[VoiceCallInvocationItems.ContactContentType]);
    }

    [Fact]
    public void PutCallIdentity_LeavesOutWhatTheActivityDoesNotHave()
    {
        // Arrange
        // A caller matched to no contact, or to more than one, has no contact on the activity: a tool must not find
        // an empty one and take it for somebody.
        var items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            PreferredDestination = " +19305550100 ",
            ContactContentItemId = "",
        };

        // Act
        RealtimeVoiceConversationRunner.PutCallIdentity(items, activity);

        // Assert
        Assert.Equal("activity-1", items[VoiceCallInvocationItems.ActivityId]);
        Assert.Equal("+19305550100", items[VoiceCallInvocationItems.CustomerNumber]);
        Assert.False(items.ContainsKey(VoiceCallInvocationItems.ContactContentItemId));
        Assert.False(items.ContainsKey(VoiceCallInvocationItems.ContactContentType));
        Assert.False(items.ContainsKey(VoiceCallInvocationItems.ChannelEndpointId));
    }
}
