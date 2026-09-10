using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges provider commands that have reached a terminal state, measured from completion. Neither the
/// retry time nor the lease time can serve as the age because neither advances once a command finishes.
/// </summary>
public sealed class ProviderCommandRetentionPolicy : ContactCenterRetentionPolicyBase<ProviderCommand, ProviderCommandIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="commandStore">The provider command store.</param>
    public ProviderCommandRetentionPolicy(
        ISession session,
        IProviderCommandStore commandStore)
        : base(session, commandStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ProviderCommand";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.ProviderCommandRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ProviderCommandIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.CompletedUtc != null
            && index.CompletedUtc < cutoffUtc
            && (index.Status == ProviderCommandStatus.Confirmed
                || index.Status == ProviderCommandStatus.Compensated
                || index.Status == ProviderCommandStatus.Failed);
}
