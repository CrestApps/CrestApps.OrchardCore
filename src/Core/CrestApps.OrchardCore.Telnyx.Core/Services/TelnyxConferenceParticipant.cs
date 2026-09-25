namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A conference participant as Telnyx lists it (<c>GET /conferences/{id}/participants</c>).
/// </summary>
/// <param name="CallControlId">The participant's call.</param>
/// <param name="Status">Where it stands: <c>joining</c>, <c>joined</c> or <c>left</c>.</param>
public sealed record TelnyxConferenceParticipant(string CallControlId, string Status);
