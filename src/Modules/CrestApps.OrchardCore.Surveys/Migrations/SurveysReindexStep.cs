using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves again, a page per run, every survey indexed before the survey index held its user owner and anonymity, so those
/// columns and the survey tag index are filled from each survey. Until a survey is reached, readers take what they need
/// from the survey itself. <see cref="SurveysMigrations"/> asks for it when it adds the columns.
/// </summary>
/// <remarks>
/// A survey is saved only when nobody saved it after the run read its index row (<see cref="SurveyUpgradePages.SaveUnchangedAsync"/>):
/// when an author saves a survey of the page while the run holds it, the run leaves it as it is rather than overwrite
/// the edit with the copy it loaded; the survey the author saved was reindexed by that save.
/// </remarks>
internal sealed class SurveysReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.SurveysReindex";

    private const char RangeSeparator = ':';

    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysReindexStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    public SurveysReindexStep(ISession session, SurveyStore surveyStore)
    {
        _session = session;
        _surveyStore = surveyStore;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (afterId, upToId) = ReadRange(context.Cursor);

        if (upToId == 0)
        {
            // A saved survey's index row is written again under a new id (and no longer waits), so the walk stops at the
            // last row that waited when it began.
            var last = await _session.QueryIndex<SurveyIndex>(index => index.Anonymity == null, collection: SurveysConstants.CollectionName)
                .OrderByDescending(index => index.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (last is null)
            {
                return true;
            }

            upToId = last.Id;
        }

        var rows = (await _session.QueryIndex<SurveyIndex>(index => index.Anonymity == null && index.Id > afterId && index.Id <= upToId, collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(SurveyUpgradePages.PageSize)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count == 0)
        {
            return true;
        }

        var ids = rows.Select(row => row.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        await SurveyUpgradePages.SaveUnchangedAsync(_session, _surveyStore, SurveysConstants.CollectionName, rows, await _surveyStore.GetAsync(ids, cancellationToken), cancellationToken);

        afterId = rows[^1].Id;

        if (rows.Count < SurveyUpgradePages.PageSize || afterId >= upToId)
        {
            return true;
        }

        context.Cursor = afterId.ToString(CultureInfo.InvariantCulture) + RangeSeparator + upToId.ToString(CultureInfo.InvariantCulture);

        return false;
    }

    private static (long AfterId, long UpToId) ReadRange(string cursor)
    {
        var separator = string.IsNullOrEmpty(cursor) ? -1 : cursor.IndexOf(RangeSeparator, StringComparison.Ordinal);

        if (separator <= 0
            || !long.TryParse(cursor.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var afterId)
            || !long.TryParse(cursor.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var upToId))
        {
            return (0, 0);
        }

        return (afterId, upToId);
    }
}
