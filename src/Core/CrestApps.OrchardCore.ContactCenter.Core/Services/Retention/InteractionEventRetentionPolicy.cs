using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges durable interaction events by the time they occurred. The event log is the only table
/// projections can be rebuilt from, so it is the only one held by the projection replay horizon.
/// </summary>
public sealed class InteractionEventRetentionPolicy : ContactCenterRetentionPolicyBase<InteractionEvent, InteractionEventIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="eventStore">The interaction event store.</param>
    public InteractionEventRetentionPolicy(
        ISession session,
        IInteractionEventStore eventStore)
        : base(session, eventStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "InteractionEvent";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override bool IsSubjectToReplayHorizon => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.InteractionEventRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<InteractionEventIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.OccurredUtc < cutoffUtc;
}
