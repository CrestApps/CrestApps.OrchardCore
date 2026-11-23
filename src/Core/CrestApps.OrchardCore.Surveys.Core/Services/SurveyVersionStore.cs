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
