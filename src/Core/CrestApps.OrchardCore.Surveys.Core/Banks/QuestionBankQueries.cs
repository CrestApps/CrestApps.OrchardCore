using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Reads question banks and their questions through their indexes, a page at a time.
/// </summary>
public sealed class QuestionBankQueries : IQuestionBankQueries
{
    private const string Collection = SurveysConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankQueries"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public QuestionBankQueries(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<QuestionBank> Items)> ListBanksAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        IQuery<QuestionBank, QuestionBankIndex> query;

        if (string.IsNullOrWhiteSpace(search))
        {
            query = _session.Query<QuestionBank, QuestionBankIndex>(collection: Collection);
        }
        else
        {
            // A search inside titles can't seek, so it goes without the grouping (see AGENTS.md, YesSql index declarations).
            var text = search.Trim();
            query = _session.Query<QuestionBank>(collection: Collection)
                .NoIndexOptimizations()
                .With<QuestionBankIndex>(index => index.Title.Contains(text) || index.Name.Contains(text));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(index => index.Title)
            .ThenBy(index => index.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, SurveysConstants.PageSize))
            .ListAsync(cancellationToken);

        return (count, items.ToList());
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<BankQuestion> Items)> ListQuestionsAsync(BankQuestionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.BankId))
        {
            return (0, []);
        }

        var bankId = query.BankId;
        var root = _session.Query<BankQuestion>(collection: Collection);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // A search inside question texts can't seek, so it goes without the grouping.
            root = root.NoIndexOptimizations();
        }

        var questions = root.With<BankQuestionIndex>(index => index.BankId == bankId);

        if (!string.IsNullOrEmpty(query.ControlType))
        {
            var controlType = query.ControlType;
            questions = questions.Where(index => index.ControlType == controlType);
        }

        if (query.Difficulty is { } difficulty)
        {
            questions = questions.Where(index => index.Difficulty == difficulty);
        }

        if (query.Retired is { } retired)
        {
            questions = questions.Where(index => index.Retired == retired);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            var rows = await _session.QueryIndex<BankQuestionTagIndex>(index => index.BankId == bankId && index.Tag == tag, collection: Collection)
                .ListAsync(cancellationToken);
            var ids = rows.Select(row => row.BankQuestionId).Distinct(StringComparer.Ordinal).ToArray();

            if (ids.Length == 0)
            {
                return (0, []);
            }

            questions = questions.Where(index => index.ItemId.IsIn(ids));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim();
            questions = questions.Where(index => index.Title.Contains(text));
        }

        var count = await questions.CountAsync(cancellationToken);
        var items = await questions
            .OrderByDescending(index => index.ModifiedUtc)
            .ThenBy(index => index.Id)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, SurveysConstants.PageSize))
            .ListAsync(cancellationToken);

        return (count, items.ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListTagsAsync(string bankId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId))
        {
            return [];
        }

        var rows = await _session.QueryIndex<BankQuestionTagIndex>(index => index.BankId == bankId, collection: Collection)
            .ListAsync(cancellationToken);

        return rows
            .Select(row => row.Tag)
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountQuestionsAsync(string bankId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankId))
        {
            return 0;
        }

        return await _session.QueryIndex<BankQuestionIndex>(index => index.BankId == bankId, collection: Collection)
            .CountAsync(cancellationToken);
    }
}
