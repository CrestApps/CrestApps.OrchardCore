namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How much of one of the assistant's spoken items the caller actually heard before they talked over it.
/// </summary>
/// <param name="ItemId">The provider's identifier for the item.</param>
/// <param name="AudioEndMilliseconds">How far into the item's audio playback had reached, in milliseconds.</param>
internal readonly record struct AssistantAudioTruncation(string ItemId, int AudioEndMilliseconds);
