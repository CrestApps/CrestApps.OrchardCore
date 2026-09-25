using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides what a waiting caller hears next: the welcome once, the callback offer while they still have the
/// patience to accept it, and the periodic update after that.
/// </summary>
public static class QueueTreatmentPolicy
{
    /// <summary>
    /// Returns the next step due for a waiting caller, or <see cref="QueueTreatmentStep.None"/>.
    /// </summary>
    /// <param name="item">The waiting item.</param>
    /// <param name="options">The queue's treatment options.</param>
    /// <param name="nowUtc">The current instant.</param>
    public static QueueTreatmentStep GetNextStep(QueueItem item, QueueTreatmentSettings options, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.WelcomeMessage) && item.TreatmentStepsPlayed == 0)
        {
            return new QueueTreatmentStep(QueueTreatmentStepKind.Welcome, options.WelcomeMessage.Trim(), null);
        }

        // Music used to start only behind the welcome, so a queue that plays music and says nothing left the
        // caller in silence for their whole wait — which is indistinguishable from a dropped call. Start it on
        // its own when there is no greeting to start it behind.
        if (!string.IsNullOrWhiteSpace(options.HoldMusicMediaId) && item.TreatmentStepsPlayed == 0)
        {
            return new QueueTreatmentStep(QueueTreatmentStepKind.HoldMusic, null, null);
        }

        var waitedSeconds = (nowUtc - item.QueueEnteredUtc).TotalSeconds;

        // The offer comes before the first announcement, because it is only useful while the caller still has
        // the patience to accept it; making them sit through updates first defeats the point of offering.
        if (!string.IsNullOrWhiteSpace(options.CallbackDtmfKey) &&
            item.CallbackOfferedUtc is null &&
            waitedSeconds >= options.CallbackOfferAfterSeconds)
        {
            return new QueueTreatmentStep(QueueTreatmentStepKind.CallbackOffer, null, options.CallbackDtmfKey.Trim());
        }

        if (options.AnnouncementIntervalSeconds <= 0)
        {
            return QueueTreatmentStep.None;
        }

        var since = item.LastTreatmentUtc ?? item.QueueEnteredUtc;

        return (nowUtc - since).TotalSeconds >= options.AnnouncementIntervalSeconds
            ? new QueueTreatmentStep(QueueTreatmentStepKind.Announcement, null, null)
            : QueueTreatmentStep.None;
    }

    /// <summary>
    /// Returns when <see cref="GetNextStep"/> will next have something for a waiting caller, or <see langword="null"/>
    /// when nothing more is ever due. A time at or before now means a step is due already.
    /// </summary>
    /// <param name="item">The waiting item.</param>
    /// <param name="options">The queue's treatment options.</param>
    public static DateTime? GetNextDueUtc(QueueItem item, QueueTreatmentSettings options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        if (item.TreatmentStepsPlayed == 0 &&
            (!string.IsNullOrWhiteSpace(options.WelcomeMessage) || !string.IsNullOrWhiteSpace(options.HoldMusicMediaId)))
        {
            return item.QueueEnteredUtc;
        }

        DateTime? dueUtc = null;

        if (!string.IsNullOrWhiteSpace(options.CallbackDtmfKey) && item.CallbackOfferedUtc is null)
        {
            dueUtc = item.QueueEnteredUtc.AddSeconds(Math.Max(0, options.CallbackOfferAfterSeconds));
        }

        // An announcement with nothing to say is never played, so a cadence without content is nothing to wait for.
        if (options.AnnouncementIntervalSeconds > 0 && (options.AnnouncePosition || options.AnnounceEstimatedWait))
        {
            var announcementDueUtc = (item.LastTreatmentUtc ?? item.QueueEnteredUtc).AddSeconds(options.AnnouncementIntervalSeconds);

            if (dueUtc is null || announcementDueUtc < dueUtc)
            {
                dueUtc = announcementDueUtc;
            }
        }

        return dueUtc;
    }
}
