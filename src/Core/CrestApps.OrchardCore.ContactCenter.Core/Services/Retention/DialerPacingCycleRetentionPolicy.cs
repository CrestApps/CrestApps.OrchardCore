using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Ages dialer pacing cycle records from when the latest cycle each one stands for ended. A record describes what the
/// dialer already did, carries no customer data, and is only ever folded into while its queue keeps ending cycles the
/// same way, so it is settled once it stops being the latest.
/// </summary>
public sealed class DialerPacingCycleRetentionPolicy : ContactCenterRetentionPolicyBase<DialerPacingCycle, DialerPacingCycleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="cycleStore">The dialer pacing cycle store.</param>
    public DialerPacingCycleRetentionPolicy(
        ISession session,
        IDialerPacingCycleStore cycleStore)
        : base(session, cycleStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "DialerPacingCycle";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.DialerPacingCycleRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<DialerPacingCycleIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.CompletedUtc < cutoffUtc;
}
