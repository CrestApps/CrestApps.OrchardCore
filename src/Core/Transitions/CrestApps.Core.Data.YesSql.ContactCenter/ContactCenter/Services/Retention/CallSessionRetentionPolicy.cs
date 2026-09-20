using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;
using CrestApps.Core.ContactCenter.Services.Retention;
using CrestApps.Core.ContactCenter.Services;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services.Retention;

/// <summary>
/// Purges call sessions that have ended, measured from the time the call ended rather than the time it
/// was created so a long call is not purged the moment it finishes.
/// </summary>
public sealed class CallSessionRetentionPolicy : ContactCenterRetentionPolicyBase<CallSession, CallSessionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="callSessionStore">The call session store.</param>
    public CallSessionRetentionPolicy(
        ISession session,
        ICallSessionStore callSessionStore)
        : base(session, callSessionStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "CallSession";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.CallSessionRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<CallSessionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.EndedUtc != null && index.EndedUtc < cutoffUtc;
}
