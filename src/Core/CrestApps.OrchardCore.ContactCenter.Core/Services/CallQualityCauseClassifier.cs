using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the most likely cause of poor call quality from what was measured.
/// </summary>
/// <remarks>
/// The rating says a call was poor; this says which measurement made it so, in the order a support engineer would
/// check them. It is a first pointer, not a diagnosis: the stored measurement is there to confirm it.
/// </remarks>
public static class CallQualityCauseClassifier
{
    /// <summary>
    /// The loss, as a percentage, from which loss alone makes a call poor.
    /// </summary>
    public const double LossPercentThreshold = 5.0;

    /// <summary>
    /// The jitter, in milliseconds, from which audio is heard to break up.
    /// </summary>
    public const double JitterMsThreshold = 30.0;

    /// <summary>
    /// The round-trip time, in milliseconds, from which people start talking over each other.
    /// </summary>
    public const double RoundTripMsThreshold = 300.0;

    /// <summary>
    /// Reads the most likely cause of one leg's quality.
    /// </summary>
    /// <param name="record">The leg's record.</param>
    /// <returns>The cause, or <see cref="CallQualityCause.Unknown"/> when the leg was not poor or nothing points at one.</returns>
    public static CallQualityCause Classify(CallQualityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Rating != CallQualityRating.Poor)
        {
            return CallQualityCause.Unknown;
        }

        if (record.Source == CallQualitySource.Provider && record.LegRole == CallPartyRole.Customer)
        {
            return CallQualityCause.CustomerSide;
        }

        if (record.Browser is { BytesReceived: 0, PacketsReceived: > 0 })
        {
            return CallQualityCause.NoAudioReceived;
        }

        // The caller could not hear the agent: the soft phone saw nothing leave, or the provider received nothing on the
        // agent's leg while sending the agent the caller.
        if ((record.Browser is { } browser && TelephonyCallQualityEvaluator.SentNoAudio(browser)) ||
            (record.Source == CallQualitySource.Provider && record.LegRole == CallPartyRole.Agent && record.Provider is { ReceivedNoAudio: true }))
        {
            return CallQualityCause.NoAudioSent;
        }

        if (record.LossPercent >= LossPercentThreshold)
        {
            return CallQualityCause.PacketLoss;
        }

        if (record.JitterMs >= JitterMsThreshold)
        {
            return CallQualityCause.Jitter;
        }

        if (record.RoundTripMs >= RoundTripMsThreshold)
        {
            return CallQualityCause.Latency;
        }

        return CallQualityCause.Unknown;
    }

    /// <summary>
    /// Reads the cause most of a set of poor legs share.
    /// </summary>
    /// <param name="records">The legs.</param>
    /// <returns>The commonest known cause, or <see cref="CallQualityCause.Unknown"/> when none is known.</returns>
    public static CallQualityCause ClassifyMostLikely(IEnumerable<CallQualityRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Select(Classify)
            .Where(cause => cause != CallQualityCause.Unknown)
            .GroupBy(cause => cause)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();
    }
}
