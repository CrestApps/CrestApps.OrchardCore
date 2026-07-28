using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges routing work state that has not been touched for the configured window. Nothing in the product ever
/// deletes a work state, so without this the table grows by one row for every activity that is ever routed.
/// There is no terminal assignment status to key on, because whether the work is finished is owned by the CRM
/// activity rather than by the routing document. Purging by age alone is safe here only because the work state
/// is reconstructible: a missing one is recreated on next access and seeded from the activity projection, which
/// is the same adoption path a tenant that predates the document takes.
/// </summary>
public sealed class ContactCenterWorkStateRetentionPolicy : ContactCenterRetentionPolicyBase<ContactCenterWorkState, ContactCenterWorkStateIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="workStateStore">The work state store.</param>
    public ContactCenterWorkStateRetentionPolicy(
        ISession session,
        IContactCenterWorkStateStore workStateStore)
        : base(session, workStateStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "ContactCenterWorkState";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.WorkStateRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<ContactCenterWorkStateIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.ModifiedUtc != null && index.ModifiedUtc < cutoffUtc;
}
