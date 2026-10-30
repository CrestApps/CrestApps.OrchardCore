using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A call handed to another AI profile speaks in that profile's voice, not in the voice chosen for the call it started
/// as. Live, the profile the caller was transferred to answered in the first one's voice, and the caller could not
/// tell they had been transferred.
/// </summary>
public sealed class SwitchedProfileVoiceTests
{
    [Fact]
    public async Task ATurnBasedCall_HandedToAProfile_SpeaksInItsOwnVoice_NotTheCampaigns()
    {
        // Arrange
        var handedOver = new OmnichannelActivity { ItemId = "activity-1", AIProfileId = "profile-service", TextToSpeechVoiceId = "campaign-voice" };
        handedOver.Put(new AIProfileSwitch { ProfileId = "profile-service" });
        var notHandedOver = new OmnichannelActivity { ItemId = "activity-2", AIProfileId = "profile-sales", TextToSpeechVoiceId = "campaign-voice" };

        // Act
        var handedTo = await VoiceAgentConversationLoop.SelectCallSpeechAsync(null, handedOver, null, new AIProfile { ItemId = "profile-service" }, "provider-voice", TestContext.Current.CancellationToken);
        var startedAs = await VoiceAgentConversationLoop.SelectCallSpeechAsync(null, notHandedOver, null, new AIProfile { ItemId = "profile-sales" }, "provider-voice", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("provider-voice", handedTo.Voice);
        Assert.Equal("campaign-voice", startedAs.Voice);
    }

    [Fact]
    public async Task ARealtimeCall_HandedToAProfile_SpeaksInItsOwnVoice_NotTheCampaigns()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", TextToSpeechVoiceId = "campaign-voice" };
        activity.Put(new AIProfileSwitch { ProfileId = "profile-service" });

        var service = new AIProfile { ItemId = "profile-service" };
        service.AlterSettings<ChatModeProfileSettings>(settings => settings.VoiceName = "alex-voice");

        // Act
        var (handedTo, _) = await RealtimeVoiceConversationRunner.SelectCallSpeechAsync(null, new RealtimeVoiceConversationContext { Activity = activity, Profile = service }, TestContext.Current.CancellationToken);
        var (startedAs, _) = await RealtimeVoiceConversationRunner.SelectCallSpeechAsync(null, new RealtimeVoiceConversationContext { Activity = activity, Profile = new AIProfile { ItemId = "profile-sales" } }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("alex-voice", handedTo);
        Assert.Equal("campaign-voice", startedAs);
    }
}
