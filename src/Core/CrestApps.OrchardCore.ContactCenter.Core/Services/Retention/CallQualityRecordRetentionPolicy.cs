using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Ages call quality records from the end of the leg they measured. A record is written once the leg is over and never
/// changed, so it is settled the moment it exists.
/// </summary>
public sealed class CallQualityRecordRetentionPolicy : ContactCenterRetentionPolicyBase<CallQualityRecord, CallQualityRecordIndex>
{
    public CallQualityRecordRetentionPolicy(
        ISession session,
        ICallQualityRecordStore recordStore)
        : base(session, recordStore)
    {
    }

    public override string EntityName => "CallQualityRecord";

    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.CallQualityRecordRetentionDays;

    protected override Expression<Func<CallQualityRecordIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ObservedUtc < cutoffUtc;
}
