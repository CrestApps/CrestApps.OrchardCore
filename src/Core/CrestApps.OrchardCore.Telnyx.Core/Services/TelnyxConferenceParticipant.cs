namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A participant of a Telnyx conference, as the participants list reports it.
/// </summary>
/// <param name="CallControlId">The participant's leg.</param>
/// <param name="Status">Its status in the conference: <c>joining</c> or <c>joined</c>.</param>
/// <param name="Muted">Whether Telnyx has it muted.</param>
/// <param name="OnHold">Whether Telnyx has it on hold.</param>
/// <param name="WhisperCallControlIds">The legs it is heard by when it is a whispering supervisor, or <see langword="null"/> when Telnyx did not say.</param>
public sealed record TelnyxConferenceParticipant(
    string CallControlId,
    string Status,
    bool Muted,
    bool OnHold,
    IReadOnlyList<string> WhisperCallControlIds);
