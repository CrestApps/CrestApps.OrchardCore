using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Who a live call is with, put where the session's tools can read it.
/// </summary>
/// <remarks>
/// Split from the runner because the runner's file is near the size the architecture guard allows.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// Puts the call's identity, from its activity, into the tool scope under <see cref="VoiceCallInvocationItems"/>.
    /// </summary>
    /// <remarks>
    /// A tool the profile gives the session, such as one that looks the caller up or records what they asked for, has
    /// to know which call it is acting for. Without this the only way it could tell was from what the caller said,
    /// which anybody can say. Values the activity does not have are left out rather than set empty.
    /// </remarks>
    /// <param name="items">The tool scope's items, or <see langword="null"/> when there is no scope.</param>
    /// <param name="activity">The call's activity.</param>
    internal static void PutCallIdentity(IDictionary<string, object> items, OmnichannelActivity activity)
    {
        if (items is null || activity is null)
        {
            return;
        }

        Put(items, VoiceCallInvocationItems.ActivityId, activity.ItemId);
        Put(items, VoiceCallInvocationItems.CustomerNumber, activity.PreferredDestination);
        Put(items, VoiceCallInvocationItems.ChannelEndpointId, activity.ChannelEndpointId);
        Put(items, VoiceCallInvocationItems.ContactContentItemId, activity.ContactContentItemId);
        Put(items, VoiceCallInvocationItems.ContactContentType, activity.ContactContentType);
    }

    private static void Put(IDictionary<string, object> items, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            items[key] = value.Trim();
        }
    }
}
