using System.Text.Json;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Edits the filters of every Surveys workflow event.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class SurveyEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, SurveyEventActivityViewModel>
    where TActivity : EventActivity
{
    private static readonly JsonSerializerOptions _ruleOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private const int MaxSurveyOptions = 500;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyEventActivityDisplayDriver(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, SurveyEventActivityViewModel model)
    {
        IList<string> surveyIds = [];

        switch (activity)
        {
            case SurveyResponseEventActivity response:
                surveyIds = response.SurveyIds;
                model.IsResponseEvent = true;
                model.Kinds = Join(response.Kinds);
                model.CollectorIds = Join(response.CollectorIds);
                model.ContextKinds = Join(response.ContextKinds);
                model.IncludeWorkflowChanges = response.IncludeWorkflowChanges;
                break;
            case SurveyEventActivity survey:
                surveyIds = survey.SurveyIds;
                model.Kinds = Join(survey.Kinds);
                break;
        }

        model.Names = activity switch
        {
            SurveyPageCompletedEvent page => Join(page.PageNames),
            SurveyResponseScreenedOutEvent screened => Join(screened.EndingNames),
            SurveyQuotaReachedEvent quota => Join(quota.QuotaNames),
            _ => null,
        };

        if (activity is SurveyResponseMatchesConditionEvent condition && condition.Rule is not null)
        {
            model.RuleJson = JsonSerializer.Serialize(condition.Rule, _ruleOptions);
        }

        model.SurveyIds = surveyIds.ToArray();

        // The first surveys by title, and the surveys the event names, read from the survey index (no survey is loaded).
        var rows = (await _session.QueryIndex<SurveyIndex>(collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Title)
            .ThenBy(index => index.Id)
            .Take(MaxSurveyOptions)
            .ListAsync()).ToList();
        var missing = model.SurveyIds.Except(rows.Select(row => row.ItemId), StringComparer.Ordinal).ToArray();

        foreach (var chunk in SurveyIdChunks.Of(missing))
        {
            rows.AddRange(await _session.QueryIndex<SurveyIndex>(index => index.ItemId.IsIn(chunk), collection: SurveysConstants.CollectionName).ListAsync());
        }

        model.SurveyOptions = rows
            .OrderBy(row => row.Title ?? row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new SelectListItem(row.Title ?? row.Name, row.ItemId, model.SurveyIds.Contains(row.ItemId)))
            .ToList();
    }

    /// <inheritdoc />
    protected override void UpdateActivity(SurveyEventActivityViewModel model, TActivity activity)
    {
        var surveyIds = (model.SurveyIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();

        switch (activity)
        {
            case SurveyResponseEventActivity response:
                response.SurveyIds = surveyIds;
                response.Kinds = Split(model.Kinds);
                response.CollectorIds = Split(model.CollectorIds);
                response.ContextKinds = Split(model.ContextKinds);
                response.IncludeWorkflowChanges = model.IncludeWorkflowChanges;
                break;
            case SurveyEventActivity survey:
                survey.SurveyIds = surveyIds;
                survey.Kinds = Split(model.Kinds);
                break;
        }

        switch (activity)
        {
            case SurveyPageCompletedEvent page:
                page.PageNames = Split(model.Names);
                break;
            case SurveyResponseScreenedOutEvent screened:
                screened.EndingNames = Split(model.Names);
                break;
            case SurveyQuotaReachedEvent quota:
                quota.QuotaNames = Split(model.Names);
                break;
            case SurveyResponseMatchesConditionEvent condition:
                condition.Rule = ParseRule(model.RuleJson);
                break;
        }
    }

    /// <summary>
    /// Reads a rule written as JSON; text that is not a rule gives no rule.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    internal static ConditionRule ParseRule(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConditionRule>(json, _ruleOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Join(IEnumerable<string> values)
        => string.Join(", ", values ?? []);

    private static List<string> Split(string values)
        => (values ?? string.Empty)
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
