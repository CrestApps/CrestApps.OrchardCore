namespace CrestApps.OrchardCore.Omnichannel.Voice;

/// <summary>
/// The keys under which an AI call held as a live session puts the call's identity into the tool scope
/// (<c>AIInvocationScope.Current.Items</c>), so a tool knows which call it is acting for from the call itself.
/// </summary>
/// <remarks>
/// A tool on a phone call must never learn who it is talking to from what the caller says: anybody can say a name or
/// read out a number. These are taken from the call's activity when the session opens, and stay for the whole call.
/// Each value is a <see cref="string"/>, and a key is absent when the activity has no value for it. The keys are
/// plain strings, so a tool in another assembly can read them without referencing this one.
/// </remarks>
public static class VoiceCallInvocationItems
{
    /// <summary>
    /// The id of the call's activity.
    /// </summary>
    public const string ActivityId = "Omnichannel.Voice.ActivityId";

    /// <summary>
    /// The customer's phone number on the call: the caller on an inbound call, the number called on an outbound one.
    /// </summary>
    public const string CustomerNumber = "Omnichannel.Voice.CustomerNumber";

    /// <summary>
    /// The id of the channel endpoint, the business's own number, that the call came in on or was placed from.
    /// </summary>
    public const string ChannelEndpointId = "Omnichannel.Voice.ChannelEndpointId";

    /// <summary>
    /// The content item id of the contact the call was matched to, set only when it was matched to exactly one.
    /// </summary>
    public const string ContactContentItemId = "Omnichannel.Voice.ContactContentItemId";

    /// <summary>
    /// The content type of the contact the call was matched to.
    /// </summary>
    public const string ContactContentType = "Omnichannel.Voice.ContactContentType";
}
