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
    private readonly SurveyContentSanitizer _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankQuestionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="sanitizer">Sanitizes the question's HTML texts on every save; the module registers it.</param>
    public BankQuestionStore(ISession session, SurveyContentSanitizer sanitizer = null)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
        _sanitizer = sanitizer;
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(BankQuestion record)
    {
        _sanitizer?.Sanitize(record?.Question);

        return base.SavingAsync(record);
    }

    /// <summary>
    /// Saves a bank question again as loaded, with the checks of every save, and with YesSql's concurrency check: when another
    /// save changed a bank question after it was loaded, the commit fails with a <see cref="ConcurrencyException"/> and writes
    /// nothing, so a background rewrite (such as the content sanitizing step) never overwrites it with the copy it loaded.
    /// </summary>
    /// <param name="record">The record, loaded in this session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async ValueTask SaveUnchangedAsync(BankQuestion record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await SavingAsync(record);
        await Session.SaveAsync(record, checkConcurrency: true, collection: CollectionName, cancellationToken: cancellationToken);
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
    /// Lists questions by id, a chunk of ids at a time (<see cref="SurveyIdChunks"/>), so a long list never passes SQL
    /// Server's limit of 2,100 parameters.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions.</returns>
    public async Task<IReadOnlyList<BankQuestion>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var items = new List<BankQuestion>();

        foreach (var chunk in SurveyIdChunks.Of(ids))
        {
            items.AddRange(await Session.Query<BankQuestion, BankQuestionIndex>(index => index.ItemId.IsIn(chunk), collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return items;
    }

    /// <summary>
    /// Lists the first questions a bank draw can take, by id: the bank's questions that aren't retired, of the difficulty
    /// when one is given, and among the given ids when there are any. Only <paramref name="take"/> questions are read,
    /// however many match.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="ids">The ids the questions must be among (such as those with the draw's tags), or <see langword="null"/>
    /// for any question of the bank.</param>
    /// <param name="difficulty">The difficulty, or <see langword="null"/> for any.</param>
    /// <param name="take">How many questions to read at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions, by id.</returns>
    public async Task<IReadOnlyList<BankQuestion>> ListDrawCandidatesAsync(string bankId, IReadOnlyCollection<string> ids, int? difficulty, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId) || take <= 0 || ids is { Count: 0 })
        {
            return [];
        }

        if (ids is null)
        {
            return (await Candidates(bankId, null, difficulty).OrderBy(index => index.ItemId).Take(take).ListAsync(cancellationToken))
                .OrderBy(question => question.ItemId, StringComparer.Ordinal)
                .ToList();
        }

        // The ids are read in order, a chunk at a time, until enough match: the first ids hold the first candidates.
        var result = new List<BankQuestion>();

        foreach (var chunk in SurveyIdChunks.Of(ids.Order(StringComparer.Ordinal)))
        {
            result.AddRange(await Candidates(bankId, chunk, difficulty).OrderBy(index => index.ItemId).Take(take - result.Count).ListAsync(cancellationToken));

            if (result.Count >= take)
            {
                break;
            }
        }

        return result.OrderBy(question => question.ItemId, StringComparer.Ordinal).ToList();
    }

    private IQuery<BankQuestion, BankQuestionIndex> Candidates(string bankId, string[] ids, int? difficulty)
    {
        var query = Session.Query<BankQuestion, BankQuestionIndex>(index => index.BankId == bankId && !index.Retired, collection: SurveysConstants.CollectionName);

        if (ids is not null)
        {
            query = query.Where(index => index.ItemId.IsIn(ids));
        }

        if (difficulty is { } value)
        {
            query = query.Where(index => index.Difficulty == value);
        }

        return query;
    }
}
