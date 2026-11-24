using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores survey versions. Versions are immutable: callers create them and never update them.
/// </summary>
public sealed class SurveyVersionStore : DocumentCatalog<SurveyVersion, SurveyVersionIndex>
{
    private readonly SurveyContentSanitizer _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="sanitizer">Sanitizes the definition's HTML texts on every save; the module registers it.</param>
    public SurveyVersionStore(ISession session, SurveyContentSanitizer sanitizer = null)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
        _sanitizer = sanitizer;
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(SurveyVersion record)
    {
        _sanitizer?.Sanitize(record?.Definition);

        return base.SavingAsync(record);
    }

    /// <summary>
    /// Saves a version again as loaded, with the checks of every save, and with YesSql's concurrency check: when another
    /// save changed the version after it was loaded, the commit fails with a <see cref="ConcurrencyException"/>
    /// and writes nothing, so a background rewrite (such as a reindex) never overwrites it.
    /// </summary>
    /// <param name="record">The version, loaded in this session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async ValueTask SaveUnchangedAsync(SurveyVersion record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await SavingAsync(record);
        await Session.SaveAsync(record, checkConcurrency: true, collection: CollectionName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists the versions of a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions.</returns>
    public async Task<IReadOnlyList<SurveyVersion>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        var items = await Session.Query<SurveyVersion, SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Reads a page of the index rows of a survey's versions, newest first, and how many versions it has; no version
    /// is loaded.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count and the rows.</returns>
    public async Task<(int Count, IReadOnlyList<SurveyVersionIndex> Rows)> PageRowsAsync(string surveyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return (0, []);
        }

        var query = Session.QueryIndex<SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var rows = await Session.QueryIndex<SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .ThenByDescending(index => index.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return (count, rows.ToList());
    }

    /// <summary>
    /// Lists the index rows of a survey's versions (id and number), newest first; no version is loaded.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public async Task<IReadOnlyList<SurveyVersionIndex>> ListRowsAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        return (await Session.QueryIndex<SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .ListAsync(cancellationToken)).ToList();
    }

    /// <summary>
    /// Lists the numbers of the versions of a survey, newest first, from the version index (no version is loaded).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version numbers.</returns>
    public async Task<IReadOnlyList<int>> ListNumbersAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        var rows = await Session.QueryIndex<SurveyVersionIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderByDescending(index => index.Number)
            .ListAsync(cancellationToken);

        return rows.Select(row => row.Number).Distinct().ToList();
    }

    /// <summary>
    /// Finds a version of a survey by number.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="number">The number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    public async Task<SurveyVersion> FindByNumberAsync(string surveyId, int number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        return await Session.Query<SurveyVersion, SurveyVersionIndex>(index => index.SurveyId == surveyId && index.Number == number, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Refuses to delete a version that a response, other than a test response, is pinned to.
    /// </summary>
    /// <param name="model">The version.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">When a response is pinned to the version.</exception>
    protected override async ValueTask DeletingAsync(SurveyVersion model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var versionId = model.ItemId;
        var count = await Session.QueryIndex<SurveyResponseIndex>(index => index.SurveyVersionId == versionId && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync();

        if (count > 0)
        {
            throw new InvalidOperationException($"The survey version {versionId} has responses and cannot be deleted.");
        }

        await base.DeletingAsync(model);
    }
}
