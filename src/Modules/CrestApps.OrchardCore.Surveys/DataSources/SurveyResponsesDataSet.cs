using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per response, read newest change first so a pipeline can read only what changed. Fields of the respondent are
/// always empty for anonymous surveys, whose rows also show their times by the day only and no context id. Modules add
/// fields through <see cref="ISurveyResponseFieldProvider"/>.
/// </summary>
internal sealed class SurveyResponsesDataSet : SurveysDataSetBase<SurveyResponse, SurveyResponseIndex, SurveyResponseDataRow>
{
    private readonly ISurveyResponseFieldProvider[] _providers;
    private readonly SurveyRespondentNames _names;
    private readonly SurveyVersionStore _versions;
    private readonly SurveyCollectorStore _collectors;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _endings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _collectorNames = new(StringComparer.Ordinal);

    public SurveyResponsesDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IEnumerable<ISurveyResponseFieldProvider> providers,
        SurveyRespondentNames names,
        SurveyVersionStore versions,
        SurveyCollectorStore collectors,
        IStringLocalizer<SurveyResponsesDataSet> S)
        : base(new DataSetDescriptor("SurveyResponses", S["Survey responses"], S["One row per survey response."]), session, authorizationService, access, SurveysConstants.ResponsesCollectionName)
    {
        _providers = providers.ToArray();
        _names = names;
        _versions = versions;
        _collectors = collectors;

        var group = S["Response"].Value;
        var respondent = S["Respondent"].Value;
        var other = S["Other"].Value;

        AddField("Id", S["Response ID"], DataFieldType.Text, row => row.Response.ItemId, group).IsIdentifier = true;
        // Response numbers are one sequence for the whole site, so an anonymous survey shows the start of the random id.
        AddField("Number", S["Number"], DataFieldType.Text, row => LabelOf(row.Response), group);
        AddField("SurveyId", S["Survey ID"], DataFieldType.Text, row => row.Response.SurveyId, group).IsIdentifier = true;
        AddField("SurveyName", S["Survey"], DataFieldType.Text, row => row.Survey?.Name, group);
        // On an anonymous survey, the version, context kind and collector show only when enough completed responses
        // share them (SurveyAnonymousAttributes), and there is no attempt number, which would tie a person's responses.
        // A masked row prints "Other" even where it has no context or collector, or its real "none" would show.
        AddField("VersionNumber", S["Version"], DataFieldType.Integer, row => AttributesOf(row.Response).ShowsVersion ? (long)row.Response.VersionNumber : null, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Response.Status.ToString(), group);
        AddField("Grading", S["Grading"], DataFieldType.Text, row => row.Response.Grading.ToString(), group);
        AddField("Respondent", S["Respondent"], DataFieldType.Text, row => row.ShowsIdentity ? row.Response.Respondent?.ToString() : null, respondent);
        AddField("UserName", S["User name"], DataFieldType.Text, row => row.ShowsIdentity ? row.UserName : null, respondent);
        AddField("ContextKind", S["Context kind"], DataFieldType.Text, row => AttributesOf(row.Response).ShowsContextKind ? row.Response.Context?.Kind : other, group);
        AddField("ContextId", S["Context ID"], DataFieldType.Text, row => IsAnonymous(row.Response.SurveyId) ? null : row.Response.Context?.Id, group).IsIdentifier = true;
        AddField("Collector", S["Collector"], DataFieldType.Text, row => AttributesOf(row.Response).ShowsCollector ? row.Collector : other, group);
        AddField("Attempt", S["Attempt"], DataFieldType.Integer, row => AttemptOf(row.Response), group);
        AddField("StartedUtc", S["Started"], DataFieldType.DateTime, row => TimeOf(row.Response.SurveyId, row.Response.StartedUtc), group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, row => TimeOf(row.Response.SurveyId, row.Response.CompletedUtc), group);
        AddField("DurationSeconds", S["Duration (seconds)"], DataFieldType.Integer, row => DurationOf(row.Response), group);
        AddField("Score", S["Score"], DataFieldType.Decimal, row => row.Response.Score, group);
        AddField("MaxScore", S["Max score"], DataFieldType.Decimal, row => row.Response.MaxScore, group);
        AddField("Percent", S["Percent"], DataFieldType.Decimal, row => row.Response.Percent, group);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, row => row.Response.Passed, group);
        AddField("Ending", S["Ending"], DataFieldType.Text, row => row.Ending, group);
        AddField("IsTest", S["Test"], DataFieldType.Boolean, row => row.Response.IsTest, group);
        AddField("ModifiedUtc", S["Modified"], DataFieldType.DateTime, row => ChangedOf(row.Response), group);

        foreach (var provider in _providers)
        {
            foreach (var (name, displayName, type) in provider.DescribeFields() ?? [])
            {
                if (!string.IsNullOrEmpty(name) && !Fields.Any(field => string.Equals(field.Name, name, StringComparison.Ordinal)))
                {
                    AddField(name, displayName ?? name, type, row => row.Extra.GetValueOrDefault(name), group);
                }
            }
        }

        CompleteDescription();
    }

    protected override (string Field, Expression<Func<SurveyResponseIndex, DateTime>> Column)? DateColumn
        => ("ModifiedUtc", index => index.ModifiedUtc);

    protected override IEnumerable<SurveyResponseDataRow> GetRows(SurveyResponse document)
    {
        yield return new SurveyResponseDataRow
        {
            Response = document,
            Survey = Surveys.GetValueOrDefault(document.SurveyId ?? string.Empty),
            ShowsIdentity = ShowsIdentity(document.SurveyId),
        };
    }

    // Fills in what needs a lookup before the conditions are evaluated for an anonymous survey's group sizes, so a
    // condition on the collector, the ending or a provider's field counts the rows the consumer keeps.
    protected override async IAsyncEnumerable<SurveyResponseDataRow> CompleteAsync(SurveyResponseDataRow row, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = row.Response;

        if (row.ShowsIdentity)
        {
            row.UserName = await _names.GetUserAsync(response.UserId, cancellationToken);
        }

        if (!string.IsNullOrEmpty(response.EndingId))
        {
            if (!_endings.TryGetValue(response.SurveyVersionId ?? string.Empty, out var names))
            {
                var version = await _versions.FindByIdAsync(response.SurveyVersionId, cancellationToken);
                _endings[response.SurveyVersionId ?? string.Empty] = names = (version?.Definition?.Endings ?? [])
                    .Where(ending => !string.IsNullOrEmpty(ending?.Id))
                    .GroupBy(ending => ending.Id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Name ?? group.First().Title, StringComparer.Ordinal);
            }

            row.Ending = names.GetValueOrDefault(response.EndingId);
        }

        if (!string.IsNullOrEmpty(response.CollectorId))
        {
            if (!_collectorNames.TryGetValue(response.CollectorId, out var collector))
            {
                _collectorNames[response.CollectorId] = collector = (await _collectors.FindByIdAsync(response.CollectorId, cancellationToken))?.Name;
            }

            row.Collector = collector;
        }

        foreach (var provider in _providers)
        {
            await provider.WriteFieldsAsync(response, row.Extra, cancellationToken);
        }

        yield return row;
    }

    protected override IQueryIndex<SurveyResponseIndex> OfSurveys(IQueryIndex<SurveyResponseIndex> rows, string[] surveyIds)
        => surveyIds is null ? rows : rows.Where(index => index.SurveyId.IsIn(surveyIds));

    protected override Expression<Func<SurveyResponseIndex, bool>> AnonymousRowsOf(string surveyId)
        => index => index.SurveyId == surveyId;

    protected override SurveyResponse ResponseOf(SurveyResponseDataRow row)
        => row?.Response;

    // The survey's name is the same on every row of a survey, like its id.
    private protected override bool TryGetSurveyValue(string field, Survey survey, out object value)
    {
        if (string.Equals(field, "SurveyName", StringComparison.OrdinalIgnoreCase))
        {
            value = survey?.Name;

            return true;
        }

        return base.TryGetSurveyValue(field, survey, out value);
    }
}
