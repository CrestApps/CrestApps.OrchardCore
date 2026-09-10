using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges secure capture sessions that have reached a terminal state, measured from the last modification. A
/// collecting capture is never purged. A settled capture holds no raw sensitive value, only a masked
/// representation and a token reference, so it carries no cardholder data past its lifetime; retaining it any
/// longer serves only the audit trail, which the interaction event log already records.
/// </summary>
public sealed class SecureCaptureSessionRetentionPolicy : ContactCenterRetentionPolicyBase<SecureCaptureSession, SecureCaptureSessionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSessionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="secureCaptureSessionStore">The secure capture session store.</param>
    public SecureCaptureSessionRetentionPolicy(
        ISession session,
        ISecureCaptureSessionStore secureCaptureSessionStore)
        : base(session, secureCaptureSessionStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "SecureCaptureSession";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.SecureCaptureRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<SecureCaptureSessionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ModifiedUtc != null
            && index.ModifiedUtc < cutoffUtc
            && (index.State == SecureCaptureState.Completed
                || index.State == SecureCaptureState.Cancelled
                || index.State == SecureCaptureState.Expired);
}
