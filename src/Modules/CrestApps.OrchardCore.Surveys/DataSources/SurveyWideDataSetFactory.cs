using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// Builds one wide data set per published survey the principal may read: <c>SurveyResponses_{surveyName}</c>.
/// </summary>
internal sealed class SurveyWideDataSetFactory
{
    /// <summary>
    /// The prefix of the per-survey data set names.
    /// </summary>
    public const string Prefix = "SurveyResponses_";

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly SurveyDataSetAccess _access;
    private readonly SurveyStore _surveys;
    private readonly SurveyVersionStore _versions;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyRespondentNames _names;
    private readonly IStringLocalizer S;

    public SurveyWideDataSetFactory(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        SurveyStore surveys,
        SurveyVersionStore versions,
        ISurveyControlRegistry registry,
        SurveyRespondentNames names,
        IStringLocalizer<SurveyWideDataSet> stringLocalizer)
    {
        _session = session;
        _authorizationService = authorizationService;
        _access = access;
        _surveys = surveys;
        _versions = versions;
        _registry = registry;
        _names = names;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the data set name of a survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The name.</returns>
    public static string DataSetName(Survey survey)
        => Prefix + survey.Name;

    /// <summary>
    /// Lists the data sets of the published surveys the principal of a run may read.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data sets.</returns>
    public async Task<IReadOnlyList<IRecordDataSet>> ListAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        if (context?.User is null || !await _authorizationService.AuthorizeAsync(context.User, SurveysPermissions.ViewOwnSurveyResponses))
        {
            return [];
        }

        var dataSets = new List<IRecordDataSet>();

        foreach (var survey in (await _access.ReadableAsync(context, cancellationToken)).Values.Where(survey => survey.PublishedVersionNumber > 0 && !string.IsNullOrEmpty(survey.Name)))
        {
            dataSets.Add(await BuildAsync(survey, cancellationToken));
        }

        return dataSets;
    }

    /// <summary>
    /// Finds the data set of a survey by its data set name.
    /// </summary>
    /// <param name="name">The data set name.</param>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data set, or <see langword="null"/>.</returns>
    public async Task<IRecordDataSet> FindAsync(string name, DataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith(Prefix, StringComparison.Ordinal) || context?.User is null)
        {
            return null;
        }

        var survey = await _surveys.FindByNameAsync(name.Substring(Prefix.Length), cancellationToken);

        if (survey is null || survey.PublishedVersionNumber <= 0 || !(await _access.ReadableAsync(context, cancellationToken)).ContainsKey(survey.ItemId))
        {
            return null;
        }

        return await BuildAsync(survey, cancellationToken);
    }

    private async Task<IRecordDataSet> BuildAsync(Survey survey, CancellationToken cancellationToken)
    {
        var versions = await _versions.ListBySurveyAsync(survey.ItemId, cancellationToken);

        return new SurveyWideDataSet(survey, versions, SurveyResponseColumns.Build(versions, _registry), _names, _session, _authorizationService, _access, S);
    }
}
