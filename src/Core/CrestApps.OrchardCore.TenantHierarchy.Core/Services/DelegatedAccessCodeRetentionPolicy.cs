using System.Linq.Expressions;
using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Removes delegated sign-in codes that expired: a code is refused once it expires, so an expired one only takes up
/// room.
/// </summary>
public sealed class DelegatedAccessCodeRetentionPolicy : DocumentRetentionPolicyBase<DelegatedAccessCode, DelegatedAccessCodeIndex>
{
    // A code is kept for five minutes after it expires, as the maintenance task did, so a sign-in that is being
    // redeemed at the moment of expiry is refused rather than racing a delete.
    private static readonly double _graceDays = TimeSpan.FromMinutes(5).TotalDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessCodeRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public DelegatedAccessCodeRetentionPolicy(ISession session)
        : base(session, TenantHierarchyConstants.CollectionName)
    {
    }

    /// <inheritdoc/>
    public override string Name => "DelegatedAccessCode";

    /// <inheritdoc/>
    protected override double GetRetentionDays() => _graceDays;

    /// <inheritdoc/>
    protected override Expression<Func<DelegatedAccessCodeIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ExpiresUtc < cutoffUtc;
}
