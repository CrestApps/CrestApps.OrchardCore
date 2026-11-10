using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The offer funnel: reserving an agent for queued work, and how each offer was settled.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of one reservation transition: reserving, accepting, declining, cancelling, compensating,
    /// expiring or releasing an offer. Its <c>operation</c> tag says which.
    /// </summary>
    internal const string OfferSpanName = "crestapps.contactcenter.offer";

    /// <summary>
    /// The agent accepted the offer.
    /// </summary>
    internal const string OfferAccepted = "accepted";

    /// <summary>
    /// The agent declined the offer, or sent the caller to voicemail.
    /// </summary>
    internal const string OfferDeclined = "declined";

    /// <summary>
    /// The offer rang out.
    /// </summary>
    internal const string OfferExpired = "expired";

    /// <summary>
    /// The platform withdrew the offer, for example because the caller hung up.
    /// </summary>
    internal const string OfferCancelled = "cancelled";

    /// <summary>
    /// A newer reservation took the queue item over before this one was released.
    /// </summary>
    internal const string OfferSuperseded = "superseded";

    /// <summary>
    /// The agent's phone could not be reached.
    /// </summary>
    internal const string OfferUnreachable = "unreachable";

    /// <summary>
    /// An agent was reserved for the queue item.
    /// </summary>
    internal const string ReserveReserved = "reserved";

    /// <summary>
    /// The activity's or the agent's reservation lock was held by somebody else for the whole wait.
    /// </summary>
    internal const string ReserveLockTimeout = "lock_timeout";

    /// <summary>
    /// The queue item was gone or no longer waiting.
    /// </summary>
    internal const string ReserveQueueItemGone = "queue_item_gone";

    /// <summary>
    /// The agent was not available, or already held another reservation.
    /// </summary>
    internal const string ReserveAgentUnavailable = "agent_unavailable";

    private static readonly Counter<long> _offersSettled = Meter.CreateCounter<long>(
        "crestapps.contactcenter.offer.settled",
        "{offer}",
        "The number of offers settled, by how: accepted, declined, expired, cancelled, superseded or unreachable.");

    private static readonly Histogram<double> _offerRingDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.offer.ring.duration",
        "The time an offer rang an agent before it was settled.");

    private static readonly Counter<long> _reservationAttempts = Meter.CreateCounter<long>(
        "crestapps.contactcenter.reservation.reserve",
        "{attempt}",
        "The number of attempts to reserve an agent for queued work, by result: why work was or was not offered.");

    private static readonly Counter<long> _offerKeepAlives = Meter.CreateCounter<long>(
        "crestapps.contactcenter.offer.keepalive",
        "{extension}",
        "The number of times an agent working a preview offer kept it from ringing out, by routing kind.");

    private static readonly Counter<long> _casConflicts = Meter.CreateCounter<long>(
        "crestapps.contactcenter.cas.conflict",
        "{conflict}",
        "The number of state transitions that lost a compare-and-set race to a concurrent transition, by site.");

    /// <summary>
    /// Starts the span of one reservation transition.
    /// </summary>
    /// <param name="operation">The transition, from a bounded set: <c>reserve</c>, <c>accept</c>, <c>reject</c>,
    /// <c>cancel</c>, <c>compensate</c>, <c>expire</c> or <c>release_unreachable</c>.</param>
    /// <param name="queueId">The queue id, read for the routing kind and set on the span.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    internal static Activity StartOffer(string operation, string queueId = null)
    {
        var activity = ActivitySource.StartActivity(OfferSpanName, ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(CrestAppsOrchardCoreTelemetry.OperationTag, operation);

            if (!string.IsNullOrEmpty(queueId))
            {
                activity.SetTag(KindTag, GetRoutingKind(queueId));
                activity.SetTag("crestapps.contactcenter.queue.id", queueId);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records how one reservation attempt ended.
    /// </summary>
    /// <param name="activity">The span of the attempt, or <see langword="null"/>.</param>
    /// <param name="result">One of the <c>Reserve*</c> values.</param>
    /// <param name="queueId">The queue id, read for the routing kind only.</param>
    internal static void RecordReserve(Activity activity, string result, string queueId)
    {
        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (!_reservationAttempts.Enabled)
        {
            return;
        }

        Add(_reservationAttempts, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            { KindTag, GetRoutingKind(queueId) },
        });
    }

    /// <summary>
    /// Records how an offer was settled, and how long it rang. Called once the transition that settled it committed.
    /// </summary>
    /// <param name="activity">The span of the transition, or <see langword="null"/>.</param>
    /// <param name="outcome">One of the <c>Offer*</c> values.</param>
    /// <param name="reservation">The reservation the offer was.</param>
    /// <param name="channel">The channel of the work offered, when known.</param>
    /// <param name="settledUtc">When it was settled.</param>
    internal static void RecordOfferSettled(
        Activity activity,
        string outcome,
        ActivityReservation reservation,
        InteractionChannel? channel,
        DateTime settledUtc)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        activity?.SetTag(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);

        if (!_offersSettled.Enabled && !_offerRingDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannel(channel) },
            { KindTag, GetRoutingKind(reservation.QueueId) },
        };

        if (_offersSettled.Enabled)
        {
            Add(_offersSettled, tags);
        }

        if (reservation.CreatedUtc != default && _offerRingDuration.Enabled)
        {
            Record(_offerRingDuration, Math.Max(0, (settledUtc - reservation.CreatedUtc).TotalSeconds), tags);
        }
    }

    /// <summary>
    /// Records that an agent working a preview offer extended it.
    /// </summary>
    /// <param name="queueId">The queue id, read for the routing kind only.</param>
    internal static void RecordOfferKeepAlive(string queueId)
    {
        if (_offerKeepAlives.Enabled)
        {
            Add(_offerKeepAlives, new TagList { { KindTag, GetRoutingKind(queueId) } });
        }
    }

    /// <summary>
    /// Records that a state transition lost a compare-and-set race to a concurrent one.
    /// </summary>
    /// <param name="site">Where, from a bounded set, such as <c>reservation.accept</c> or <c>provider_event.ingest</c>.</param>
    internal static void RecordCasConflict(string site)
    {
        if (!_casConflicts.Enabled)
        {
            return;
        }

        Add(_casConflicts, new TagList
        {
            { SiteTag, site ?? CrestAppsOrchardCoreTelemetry.Unknown },
        });
    }
}
