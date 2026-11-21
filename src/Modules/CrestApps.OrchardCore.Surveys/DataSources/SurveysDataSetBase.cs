using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// The base of the Surveys data sets: a principal needs View own survey responses, and reads only the rows of the
/// surveys whose responses it may read.
/// </summary>
/// <typeparam name="TDocument">The documents read.</typeparam>
/// <typeparam name="TIndex">The index they are read through.</typeparam>
/// <typeparam name="TRow">The rows.</typeparam>
public abstract class SurveysDataSetBase<TDocument, TIndex, TRow> : IndexedReportDataSet<TDocument, TIndex, TRow>, ISurveysDataSet
    where TDocument : class
    where TIndex : MapIndex
    where TRow : class
{
    private readonly SurveyDataSetAccess _access;

    private protected SurveysDataSetBase(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        string collection)
        : base(descriptor, session, authorizationService, collection, SurveysPermissions.ViewOwnSurveyResponses)
    {
        _access = access;
    }

    /// <summary>
    /// Gets the surveys the principal of the current read may read, by id. Set when a read starts.
    /// </summary>
    protected IReadOnlyDictionary<string, Survey> Surveys { get; private set; } = new Dictionary<string, Survey>();

    /// <summary>
    /// Gets whether the principal of the current read may read who responded. Set when a read starts.
    /// </summary>
    protected bool CanSeeIdentity { get; private set; }

    /// <inheritdoc />
    protected override bool CanAggregate => false;

    /// <summary>
    /// Narrows the index rows read to those of the given surveys.
    /// </summary>
    /// <param name="rows">The index rows.</param>
    /// <param name="surveyIds">The survey ids.</param>
    /// <returns>The narrowed rows.</returns>
    protected abstract IQueryIndex<TIndex> OfSurveys(IQueryIndex<TIndex> rows, string[] surveyIds);

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        Surveys = await _access.ReadableAsync(context, cancellationToken);
        CanSeeIdentity = await _access.CanSeeIdentityAsync(context);

        if (Surveys.Count == 0)
        {
            return null;
        }

        var ids = Surveys.Keys.ToArray();

        return rows => OfSurveys(rows, ids);
    }

    /// <summary>
    /// Gets whether the rows of a survey may show who responded.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    protected bool ShowsIdentity(string surveyId)
        => CanSeeIdentity && !SurveyDataSetAccess.IsAnonymous(Surveys.GetValueOrDefault(surveyId ?? string.Empty));
}
