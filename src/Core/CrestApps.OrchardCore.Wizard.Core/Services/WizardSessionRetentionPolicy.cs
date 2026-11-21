using System.Linq.Expressions;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.Wizard.Core.Services;

/// <summary>
/// Removes pending wizard sessions nobody touched within <see cref="RetentionDays"/> days: a visitor's ownership token
/// lasts as long, so nobody can resume such a session anyway. Completed sessions are records of what was submitted and
/// are kept.
/// </summary>
public sealed class WizardSessionRetentionPolicy : DocumentRetentionPolicyBase<WizardSession, WizardSessionIndex>
{
    /// <summary>
    /// How long an untouched pending session is kept.
    /// </summary>
    public const double RetentionDays = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardSessionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public WizardSessionRetentionPolicy(ISession session)
        : base(session, collectionName: null)
    {
    }

    /// <inheritdoc/>
    public override string Name => "WizardSession";

    /// <inheritdoc/>
    protected override double GetRetentionDays() => RetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<WizardSessionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.Status == WizardSessionStatus.Pending && index.ModifiedUtc < cutoffUtc;
}
