using System.Text.Json;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

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

    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    public SurveyEventActivityDisplayDriver(SurveyStore surveyStore)
    {
        _surveyStore = surveyStore;
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

        var surveys = await _surveyStore.GetAllAsync();

        model.SurveyOptions = surveys
            .OrderBy(survey => survey.Title ?? survey.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(500)
            .Select(survey => new SelectListItem(survey.Title ?? survey.Name, survey.ItemId, model.SurveyIds.Contains(survey.ItemId)))
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
