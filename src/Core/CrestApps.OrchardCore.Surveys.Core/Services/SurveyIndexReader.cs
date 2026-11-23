using System.Linq.Expressions;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads surveys from their index rows, without loading the survey documents (drafts and all): for lists, report filters
/// and access checks that need only who owns a survey, its title and its anonymity. A row indexed before the index held
/// the anonymity is read from its survey instead, until the reindex step reaches it.
/// </summary>
/// <remarks>
/// The surveys returned are summaries: their drafts are empty and they hold no secrets. Never save one.
/// </remarks>
public sealed class SurveyIndexReader
{
    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyIndexReader"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store, for rows indexed before the index held the anonymity.</param>
    public SurveyIndexReader(ISession session, SurveyStore surveyStore)
    {
        _session = session;
        _surveyStore = surveyStore;
    }

    /// <summary>
    /// Lists survey summaries.
    /// </summary>
    /// <param name="surveyIds">The ids of the surveys, or <see langword="null"/> for every survey.</param>
    /// <param name="publishedOnly">Whether only surveys with a published version are listed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summaries.</returns>
    public async Task<IReadOnlyList<Survey>> ListAsync(IEnumerable<string> surveyIds, bool publishedOnly, CancellationToken cancellationToken = default)
    {
        var rows = new List<SurveyIndex>();

        if (surveyIds is null)
        {
            rows.AddRange(await RowsAsync(publishedOnly ? index => index.PublishedVersionNumber > 0 : null, cancellationToken));
        }
        else
        {
            foreach (var chunk in SurveyIdChunks.Of(surveyIds))
            {
                rows.AddRange(await RowsAsync(publishedOnly
                    ? index => index.ItemId.IsIn(chunk) && index.PublishedVersionNumber > 0
                    : index => index.ItemId.IsIn(chunk), cancellationToken));
            }
        }

        return await ToSurveysAsync(rows, cancellationToken);
    }

    /// <summary>
    /// Lists the ids of the surveys a user owns.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey ids.</returns>
    public async Task<IReadOnlyCollection<string>> ListIdsOwnedByAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var ids = (await RowsAsync(index => index.OwnerId == userId, cancellationToken))
            .Select(row => row.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        // Rows indexed before the index held the owner are read from their surveys.
        foreach (var survey in await NotReindexedAsync(cancellationToken))
        {
            if (string.Equals(survey.OwnerId, userId, StringComparison.Ordinal))
            {
                ids.Add(survey.ItemId);
            }
        }

        return ids;
    }

    /// <summary>
    /// Lists the ids of the surveys any of several records own, a chunk of owners at a time.
    /// </summary>
    /// <param name="owners">The owners.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The survey ids.</returns>
    public async Task<IReadOnlyCollection<string>> ListIdsByOwnersAsync(IEnumerable<RecordRef> owners, CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in SurveyIdChunks.Of((owners ?? []).Where(owner => !owner.IsEmpty).Select(owner => owner.ToString())))
        {
            foreach (var row in await RowsAsync(index => index.OwnerRef.IsIn(chunk), cancellationToken))
            {
                ids.Add(row.ItemId);
            }
        }

        return ids;
    }

    /// <summary>
    /// Makes a survey summary from its index row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The summary.</returns>
    public static Survey ToSurvey(SurveyIndex row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new Survey
        {
            ItemId = row.ItemId,
            Name = row.Name,
            Title = row.Title,
            Kind = row.Kind,
            Status = Enum.TryParse<SurveyStatus>(row.Status, out var status) ? status : default,
            Owner = RecordRef.TryParse(row.OwnerRef, out var owner) ? owner : (RecordRef?)null,
            OwnerId = row.OwnerId,
            PublishedVersionNumber = row.PublishedVersionNumber,
            ModifiedUtc = row.ModifiedUtc,
            Access = new SurveyAccessSettings
            {
                // A row without the anonymity is never made into a summary (its survey is read), so this is known.
                Anonymity = Enum.TryParse<AnonymityMode>(row.Anonymity, out var anonymity) ? anonymity : AnonymityMode.Anonymous,
                MinimumGroupSize = row.MinimumGroupSize ?? new SurveyAccessSettings().MinimumGroupSize,
            },
        };
    }

    private async Task<IReadOnlyList<Survey>> ToSurveysAsync(List<SurveyIndex> rows, CancellationToken cancellationToken)
    {
        var surveys = new List<Survey>(rows.Count);
        var stale = new List<string>();

        foreach (var row in rows)
        {
            if (row.Anonymity is null)
            {
                stale.Add(row.ItemId);
            }
            else
            {
                surveys.Add(ToSurvey(row));
            }
        }

        foreach (var chunk in SurveyIdChunks.Of(stale))
        {
            surveys.AddRange(await _surveyStore.GetAsync(chunk, cancellationToken));
        }

        return surveys;
    }

    private async Task<IReadOnlyCollection<Survey>> NotReindexedAsync(CancellationToken cancellationToken)
    {
        var stale = (await RowsAsync(index => index.Anonymity == null, cancellationToken)).Select(row => row.ItemId).ToList();
        var surveys = new List<Survey>();

        foreach (var chunk in SurveyIdChunks.Of(stale))
        {
            surveys.AddRange(await _surveyStore.GetAsync(chunk, cancellationToken));
        }

        return surveys;
    }

    private async Task<IEnumerable<SurveyIndex>> RowsAsync(Expression<Func<SurveyIndex, bool>> filter, CancellationToken cancellationToken)
    {
        var query = _session.QueryIndex<SurveyIndex>(collection: SurveysConstants.CollectionName);

        return await (filter is null ? query : query.Where(filter)).ListAsync(cancellationToken);
    }
}
