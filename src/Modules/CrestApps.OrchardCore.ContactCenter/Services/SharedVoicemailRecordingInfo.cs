using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What the shared voicemail page shows about a message's recording: whether there is one, and how long it is.
/// </summary>
internal static class SharedVoicemailRecordingInfo
{
    /// <summary>
    /// Determines whether a shared voicemail's interaction holds a stored recording.
    /// </summary>
    /// <param name="interaction">The voicemail's interaction.</param>
    /// <returns><see langword="true"/> when there is a recording to play.</returns>
    public static bool HasRecording(Interaction interaction)
        => interaction is not null &&
            !string.IsNullOrEmpty(interaction.RecordingReference) &&
            interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var reference) &&
            !string.IsNullOrEmpty(reference?.ToString());

    /// <summary>
    /// Gets the length of a shared voicemail: the provider's own length of the recording when it reported one;
    /// otherwise the time from the caller being sent to voicemail to their hanging up, which includes the greeting but
    /// is the closest the platform knows.
    /// </summary>
    /// <param name="interaction">The voicemail's interaction.</param>
    /// <param name="voicemail">The voicemail.</param>
    /// <returns>The length in seconds, or <see langword="null"/> when it is not known.</returns>
    public static int? GetDurationSeconds(Interaction interaction, SharedVoicemail voicemail)
    {
        if (interaction?.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.DurationSeconds, out var value) &&
            double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
            seconds >= 0)
        {
            return (int)Math.Round(seconds);
        }

        if (interaction?.EndedUtc is { } endedUtc && voicemail is not null && voicemail.ReceivedUtc != default && endedUtc >= voicemail.ReceivedUtc)
        {
            return (int)Math.Round((endedUtc - voicemail.ReceivedUtc).TotalSeconds);
        }

        return null;
    }
}
