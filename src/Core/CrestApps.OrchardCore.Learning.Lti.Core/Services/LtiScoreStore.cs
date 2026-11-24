using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Stores the latest score of each learner on each line item.
/// </summary>
public sealed class LtiScoreStore : DocumentCatalog<LtiScore, LtiScoreIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtiScoreStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public LtiScoreStore(ISession session)
        : base(session)
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <summary>
    /// Returns a learner's score on a line item, or <see langword="null"/>.
    /// </summary>
    /// <param name="lineItemId">The line item.</param>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The score.</returns>
    public async Task<LtiScore> FindAsync(string lineItemId, string userId, CancellationToken cancellationToken = default)
        => await Session.Query<LtiScore, LtiScoreIndex>(index => index.LineItemId == lineItemId && index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Pages the scores of a line item.
    /// </summary>
    /// <param name="lineItemId">The line item.</param>
    /// <param name="userId">Only this learner, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The total and the page.</returns>
    public async Task<(int Count, IReadOnlyList<LtiScore> Items)> PageAsync(string lineItemId, string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<LtiScore, LtiScoreIndex>(index => index.LineItemId == lineItemId, collection: CollectionName);

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(index => index.UserId == userId);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Lists every score of a learner, for personal data.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scores.</returns>
    public async Task<IReadOnlyList<LtiScore>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => (await Session.Query<LtiScore, LtiScoreIndex>(index => index.UserId == userId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists every score of a line item, to remove them with it.
    /// </summary>
    /// <param name="lineItemId">The line item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scores.</returns>
    public async Task<IReadOnlyList<LtiScore>> ListByLineItemAsync(string lineItemId, CancellationToken cancellationToken = default)
        => (await Session.Query<LtiScore, LtiScoreIndex>(index => index.LineItemId == lineItemId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
}
