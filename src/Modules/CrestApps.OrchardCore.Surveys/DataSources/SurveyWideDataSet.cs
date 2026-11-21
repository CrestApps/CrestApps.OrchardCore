using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One survey's responses, one row each, with a field per question across its versions (joined by question id, named as
/// in the newest version): the data set reports and pipelines export most.
/// </summary>
internal sealed class SurveyWideDataSet : SurveysDataSetBase<SurveyResponse, SurveyResponseIndex, SurveyResponseChildRow>
{
    private readonly Survey _survey;
    private readonly IReadOnlyDictionary<string, SurveyVersion> _versions;
    private readonly SurveyResponseColumns _columns;

    public SurveyWideDataSet(
        Survey survey,
        IReadOnlyList<SurveyVersion> versions,
        SurveyResponseColumns columns,
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer S)
        : base(
            new DataSetDescriptor(SurveyWideDataSetFactory.DataSetName(survey), S["{0} responses", survey.Title ?? survey.Name], S["One row per response to {0}, with a field per question.", survey.Title ?? survey.Name])
            {
                Group = S["Survey responses"],
            },
            session,
            authorizationService,
            access,
            SurveysConstants.ResponsesCollectionName)
    {
        _survey = survey;
        _versions = versions.ToDictionary(version => version.ItemId, StringComparer.Ordinal);
        _columns = columns;

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

        foreach (var column in columns.Columns)
        {
            if (!Fields.Any(field => string.Equals(field.Name, column.Key, StringComparison.OrdinalIgnoreCase)))
            {
                AddValue(column.Key, column.Title ?? column.Key, column.Type, answers);
            }
        }

        CompleteDescription();
    }

    protected override IEnumerable<SurveyResponseChildRow> GetRows(SurveyResponse document)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = document.ItemId,
            ["Number"] = document.Number,
            ["VersionNumber"] = (long)document.VersionNumber,
            ["Status"] = document.Status.ToString(),
            ["StartedUtc"] = DateTime.SpecifyKind(document.StartedUtc, DateTimeKind.Utc),
            ["CompletedUtc"] = document.CompletedUtc is { } completed ? DateTime.SpecifyKind(completed, DateTimeKind.Utc) : null,
            ["Score"] = document.Score,
            ["Percent"] = document.Percent,
            ["Passed"] = document.Passed,
            ["ContextKind"] = document.Context?.Kind,
            ["ContextId"] = document.Context?.Id,
            ["UserId"] = ShowsIdentity(document.SurveyId) ? document.UserId : null,
        };

        _columns.Write(document, _versions.GetValueOrDefault(document.SurveyVersionId ?? string.Empty)?.Definition, values, new SurveyFormatContext { UseValues = true });

        yield return new SurveyResponseChildRow
        {
            Response = document,
            Values = values,
        };
    }

    protected override IQueryIndex<SurveyResponseIndex> OfSurveys(IQueryIndex<SurveyResponseIndex> rows, string[] surveyIds)
    {
        var surveyId = _survey.ItemId;

        return surveyIds.Contains(surveyId, StringComparer.Ordinal)
            ? rows.Where(index => index.SurveyId == surveyId && !index.IsTest)
            : rows.Where(index => index.SurveyId == null && index.SurveyId != null);
    }

    private DataField AddValue(string name, string displayName, DataFieldType type, string group)
        => AddField(name, displayName, type, row => row.Values.GetValueOrDefault(name), group);
}
