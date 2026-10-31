namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// Kept on an AI call's activity once the call was handed to another AI profile, so the profile it was handed to
/// speaks in its own voice rather than the voice the call started with.
/// </summary>
/// <remarks>
/// A voice set on the activity or its subject flow is the campaign's choice for the call it started, and it would
/// otherwise win for every profile on the call: live, the profile the caller was transferred to answered in the first
/// one's voice, and the caller could not tell they had been transferred.
/// </remarks>
public sealed class AIProfileSwitch
{
    /// <summary>
    /// Gets or sets the profile the call was handed to.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets whether the call was handed to a profile and is still on it.
    /// </summary>
    /// <param name="activityProfileSwitch">The switch kept on the activity, if any.</param>
    /// <param name="profileId">The profile the call is on.</param>
    /// <returns><see langword="true"/> when the call was handed to <paramref name="profileId"/>.</returns>
    public static bool HandedTo(AIProfileSwitch activityProfileSwitch, string profileId)
        => activityProfileSwitch is not null &&
            !string.IsNullOrEmpty(profileId) &&
            string.Equals(activityProfileSwitch.ProfileId, profileId, StringComparison.Ordinal);
}
