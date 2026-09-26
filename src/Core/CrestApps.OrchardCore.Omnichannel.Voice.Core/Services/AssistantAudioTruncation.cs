namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How much of one of the assistant's spoken items the caller actually heard before they talked over it.
/// </summary>
/// <param name="ItemId">The provider's identifier for the item.</param>
/// <param name="AudioEndMilliseconds">How far into the item's audio playback had reached, in milliseconds.</param>
/// <param name="DeliveredMilliseconds">
/// How much of the item's audio had been delivered here by then, in milliseconds. The cut can never be later than
/// this, and it is what the cut is scaled against if the provider turns out to hold less of the item than that.
/// </param>
internal readonly record struct AssistantAudioTruncation(string ItemId, int AudioEndMilliseconds, int DeliveredMilliseconds);
