using System.Linq.Expressions;
using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Deletes sign-ups that were never confirmed: members still pending <see cref="MailingListsOptions.PendingRetentionDays"/>
/// days after they signed up (30 by default).
/// </summary>
public sealed class PendingMembersRetentionPolicy : DocumentRetentionPolicyBase<MailingListMember, MailingListMemberIndex>
{
    private readonly MailingListsOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingMembersRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="options">The mailing lists options, for the retention.</param>
    public PendingMembersRetentionPolicy(
        ISession session,
        IOptions<MailingListsOptions> options)
        : base(session, MailingListsConstants.CollectionName)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public override string Name
        => "MailingListPendingMembers";

    /// <inheritdoc/>
    protected override double GetRetentionDays()
        => _options.PendingRetentionDays;

    /// <inheritdoc/>
    protected override double GetMinimumRetentionDays()
        => 1;

    /// <inheritdoc/>
    protected override Expression<Func<MailingListMemberIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.Status == MailingListMemberStatus.Pending && index.StatusChangedUtc < cutoffUtc;
}
