using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One survey's responses, one row each, with a field per question across its versions (joined by question id, named as
/// in the newest version): the data set reports and pipelines export most. It names the respondent (display name, Guest
/// or Deleted user) on an identified survey; an anonymous survey names nobody, shows its times by the day only and
/// shows no context id.
/// </summary>
internal sealed class SurveyWideDataSet : SurveysDataSetBase<SurveyResponse, SurveyResponseIndex, SurveyResponseChildRow>
{
    private readonly Survey _survey;
    private readonly IReadOnlyDictionary<string, SurveyVersion> _versions;
    private readonly SurveyResponseColumns _columns;
    private readonly SurveyRespondentNames _names;
    private readonly SurveyDataSetAccess _access;

    public SurveyWideDataSet(
        Survey survey,
        IReadOnlyList<SurveyVersion> versions,
        SurveyResponseColumns columns,
        SurveyRespondentNames names,
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer S)
        : base(
            Describe(survey, S),
            session,
            authorizationService,
            access,
            SurveysConstants.ResponsesCollectionName)
    {
        _survey = survey;
        _versions = versions.ToDictionary(version => version.ItemId, StringComparer.Ordinal);
        _columns = columns;
        _names = names;
        _access = access;

        var group = S["Response"].Value;
        var answers = S["Answers"].Value;

        AddValue("Id", S["Response ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("Number", S["Number"], DataFieldType.Text, group);
        AddValue("VersionNumber", S["Version"], DataFieldType.Integer, group);
        AddValue("Status", S["Status"], DataFieldType.Text, group);
        AddValue("StartedUtc", S["Started"], DataFieldType.DateTime, group);
        AddValue("CompletedUtc", S["Completed"], DataFieldType.DateTime, group);
        AddValue("Score", S["Score"], DataFieldType.Decimal, group);
        AddValue("Percent", S["Percent"], DataFieldType.Decimal, group);
        AddValue("Passed", S["Passed"], DataFieldType.Boolean, group);
        AddValue("ContextKind", S["Context kind"], DataFieldType.Text, group);
        AddValue("ContextId", S["Context ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("UserId", S["User ID"], DataFieldType.Text, S["Respondent"].Value).IsIdentifier = true;
        AddValue("Respondent", S["Respondent"], DataFieldType.Text, S["Respondent"].Value);

        foreach (var column in columns.Columns)
        {
            if (!Fields.Any(field => string.Equals(field.Name, column.Key, StringComparison.OrdinalIgnoreCase)))
            {
                AddValue(column.Key, column.Title ?? column.Key, column.Type, answers);
            }
        }

        CompleteDescription();
    }

    /// <summary>
    /// Describes the data set of a survey, without reading its versions, for the list of data sets.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The descriptor.</returns>
    public static DataSetDescriptor Describe(Survey survey, IStringLocalizer S)
        => new(SurveyWideDataSetFactory.DataSetName(survey), S["{0} responses", survey.Title ?? survey.Name], S["One row per response to {0}, with a field per question.", survey.Title ?? survey.Name])
        {
            Group = S["Survey responses"],
        };

    private protected override async Task<(bool All, IReadOnlyDictionary<string, Survey> Surveys)> ReadableAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        // The data set reads one survey's responses, so only that survey is checked and read.
        IReadOnlyDictionary<string, Survey> surveys = await _access.CanReadAsync(context, _survey.ItemId, cancellationToken)
            ? new Dictionary<string, Survey>(StringComparer.Ordinal) { [_survey.ItemId] = _survey }
            : new Dictionary<string, Survey>(StringComparer.Ordinal);

        return (false, surveys);
    }

    protected override IEnumerable<SurveyResponseChildRow> GetRows(SurveyResponse document)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = document.ItemId,
            ["Number"] = SurveyAnonymity.ResponseLabel(_survey, document),
            ["VersionNumber"] = (long)document.VersionNumber,
            ["Status"] = document.Status.ToString(),
            ["StartedUtc"] = TimeOf(document.SurveyId, document.StartedUtc),
            ["CompletedUtc"] = TimeOf(document.SurveyId, document.CompletedUtc),
            ["Score"] = document.Score,
            ["Percent"] = document.Percent,
            ["Passed"] = document.Passed,
            ["ContextKind"] = document.Context?.Kind,
            ["ContextId"] = IsAnonymous(document.SurveyId) ? null : document.Context?.Id,
            ["UserId"] = ShowsIdentity(document.SurveyId) ? document.UserId : null,
        };

        _columns.Write(document, _versions.GetValueOrDefault(document.SurveyVersionId ?? string.Empty)?.Definition, values, new SurveyFormatContext { UseValues = true });

        yield return new SurveyResponseChildRow
        {
            Response = document,
            Values = values,
        };
    }

    protected override SurveyResponse ResponseOf(SurveyResponseChildRow row)
        => row?.Response;

    protected override async IAsyncEnumerable<SurveyResponseChildRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            row.Values["Respondent"] = ShowsIdentity(row.Response.SurveyId)
                ? await _names.GetUserAsync(row.Response.UserId, cancellationToken)
                : await _names.GetAsync(row.Response, Surveys.GetValueOrDefault(row.Response.SurveyId ?? string.Empty), cancellationToken);

            yield return row;
        }
    }

    protected override IQueryIndex<SurveyResponseIndex> OfSurveys(IQueryIndex<SurveyResponseIndex> rows, string[] surveyIds)
    {
        var surveyId = _survey.ItemId;

        return surveyIds is null || surveyIds.Contains(surveyId, StringComparer.Ordinal)
            ? rows.Where(index => index.SurveyId == surveyId && !index.IsTest)
            : rows.Where(index => index.SurveyId == null && index.SurveyId != null);
    }

    private DataField AddValue(string name, string displayName, DataFieldType type, string group)
        => AddField(name, displayName, type, row => row.Values.GetValueOrDefault(name), group);
}
