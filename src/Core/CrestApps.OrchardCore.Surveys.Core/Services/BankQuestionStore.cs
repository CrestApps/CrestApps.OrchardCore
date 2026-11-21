using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores the questions of question banks.
/// </summary>
public sealed class BankQuestionStore : DocumentCatalog<BankQuestion, BankQuestionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankQuestionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BankQuestionStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Lists the questions of a bank.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="includeRetired">Whether retired questions are included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions.</returns>
    public async Task<IReadOnlyList<BankQuestion>> ListByBankAsync(string bankId, bool includeRetired, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId))
        {
            return [];
        }

        var query = includeRetired
            ? Session.Query<BankQuestion, BankQuestionIndex>(index => index.BankId == bankId, collection: SurveysConstants.CollectionName)
            : Session.Query<BankQuestion, BankQuestionIndex>(index => index.BankId == bankId && !index.Retired, collection: SurveysConstants.CollectionName);

        var items = await query.OrderBy(index => index.Id).ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Finds a bank question by its import source identifier.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="sourceIdentifier">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The question, or <see langword="null"/>.</returns>
    public async Task<BankQuestion> FindBySourceAsync(string bankId, string sourceIdentifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId) || string.IsNullOrEmpty(sourceIdentifier))
        {
            return null;
        }

        return await Session.Query<BankQuestion, BankQuestionIndex>(index => index.BankId == bankId && index.SourceIdentifier == sourceIdentifier, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the ids of the questions of a bank that have every tag.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The question ids.</returns>
    public async Task<IReadOnlyCollection<string>> ListIdsWithTagsAsync(string bankId, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId) || tags is null || tags.Count == 0)
        {
            return [];
        }

        HashSet<string> result = null;

        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var rows = await Session.QueryIndex<BankQuestionTagIndex>(index => index.BankId == bankId && index.Tag == tag && !index.Retired, collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken);

            var ids = rows.Select(row => row.BankQuestionId).ToHashSet(StringComparer.Ordinal);

            if (result is null)
            {
                result = ids;
            }
            else
            {
                result.IntersectWith(ids);
            }
        }

        return (IReadOnlyCollection<string>)result ?? [];
    }

    /// <summary>
    /// Lists questions by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions.</returns>
    public async Task<IReadOnlyList<BankQuestion>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var items = await Session.Query<BankQuestion, BankQuestionIndex>(index => index.ItemId.IsIn(ids), collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
    }
}
