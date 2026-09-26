using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads a call quality record's headline figures, and a provider measurement's rating, out of the raw measurement it
/// carries.
/// </summary>
public static class CallQualityRecordFigures
{
    /// <summary>
    /// Fills the record's headline figures from its raw measurement, and rates a provider measurement from it.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The same record.</returns>
    public static CallQualityRecord Apply(CallQualityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Browser is { } browser)
        {
            var mos = browser.AvgMos > 0 ? browser.AvgMos : browser.Mos;

            record.Mos = mos > 0 ? mos : null;
            record.LossPercent = Math.Max(browser.MaxLossPercent, browser.LossPercent);
            record.JitterMs = browser.JitterMs;
            record.RoundTripMs = browser.RoundTripTimeMs > 0 ? browser.RoundTripTimeMs : null;
            record.DurationSeconds = browser.DurationMs > 0 ? browser.DurationMs / 1000.0 : null;

            return record;
        }

        // A provider measurement is rated on the provider's opinion score alone, and its headline figures are the ones
        // that rating reads: its skipped slots are not packet loss and its jitter figure is a peak variance, not a mean
        // jitter, so neither is shown as one. Records kept before that rule are read the same way, so history agrees.
        //
        // An agent's leg on which the provider received nothing has no opinion score, and is not a good call for that:
        // the caller could not hear the agent. The rating needs the leg's role, so the record is applied once it is known.
        if (record.Source == CallQualitySource.Provider && record.Provider is { } provider)
        {
            record.Mos = provider.MeasuredInboundMos;
            record.LossPercent = null;
            record.JitterMs = null;
            record.Rating = record.LegRole == CallPartyRole.Agent && provider.ReceivedNoAudio
                ? CallQualityRating.Poor
                : TelephonyCallQualityEvaluator.EvaluateProvider(provider);
        }

        return record;
    }
}
