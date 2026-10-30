namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// A request made of a live AI call.
/// </summary>
/// <param name="Kind">What is asked.</param>
/// <param name="Text">The note, for <see cref="AIVoiceCallCommandKind.Instruct"/>.</param>
public sealed record AIVoiceCallCommand(AIVoiceCallCommandKind Kind, string Text = null);
