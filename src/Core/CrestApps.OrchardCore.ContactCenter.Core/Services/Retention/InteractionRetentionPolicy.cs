using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges interactions that have ended. A live interaction is never purged no matter how old it is,
/// because age alone does not make an in-flight conversation safe to delete.
/// </summary>
public sealed class InteractionRetentionPolicy : ContactCenterRetentionPolicyBase<Interaction, InteractionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="interactionStore">The interaction store.</param>
    public InteractionRetentionPolicy(
        ISession session,
        IInteractionStore interactionStore)
        : base(session, interactionStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "Interaction";

    /// <inheritdoc/>
    protected override bool IsSubjectToLegalHold => true;

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.InteractionRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<InteractionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.EndedUtc != null && index.EndedUtc < cutoffUtc;
}
