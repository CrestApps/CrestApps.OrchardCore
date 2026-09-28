using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges callback requests that are no longer waiting, measured from the last modification. The scheduled time
/// cannot serve as the age because a callback booked weeks ahead and then canceled keeps a future scheduled
/// time, so it would never look old enough to purge. Promotion is treated as settled: nothing reads a callback
/// once it leaves the pending state, and promotion copies the destination, campaign, contact and notes onto the
/// activity, which is the durable record of the work from that point on. What does not survive the purge is the
/// agent the callback was personally reserved for and the times it was requested and booked, none of which any
/// code path reads today; the promotion event in the interaction log records that the transition happened. The
/// outcome statuses are included for completeness, but the callback aggregate has no dialing-outcome lifecycle
/// yet, so in practice a callback settles when it is promoted.
/// </summary>
public sealed class CallbackRequestRetentionPolicy : ContactCenterRetentionPolicyBase<CallbackRequest, CallbackRequestIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="callbackRequestStore">The callback request store.</param>
    public CallbackRequestRetentionPolicy(
        ISession session,
        ICallbackRequestStore callbackRequestStore)
        : base(session, callbackRequestStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "CallbackRequest";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.CallbackRequestRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<CallbackRequestIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ModifiedUtc != null
            && index.ModifiedUtc < cutoffUtc
            && (index.Status == CallbackRequestStatus.Scheduled
                || index.Status == CallbackRequestStatus.Completed
                || index.Status == CallbackRequestStatus.Canceled
                || index.Status == CallbackRequestStatus.Failed);
}
