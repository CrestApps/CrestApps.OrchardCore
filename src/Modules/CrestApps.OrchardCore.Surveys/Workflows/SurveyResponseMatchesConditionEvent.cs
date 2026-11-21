using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a completed survey response matches a rule over its answers, such as "satisfaction is 1 or 2". It turns
/// "below threshold" alerts into workflows.
/// </summary>
public sealed class SurveyResponseMatchesConditionEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseMatchesConditionEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseMatchesConditionEvent(IStringLocalizer<SurveyResponseMatchesConditionEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseMatchesConditionEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Matches Condition"];

    /// <summary>
    /// Gets or sets the rule. Its sources are question names, question ids or question.row; its values are choice values,
    /// choice ids or entered text. A rule without conditions matches every completed response.
    /// </summary>
    public ConditionRule Rule
    {
        get => GetProperty<ConditionRule>(() => null);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override bool Matches(WorkflowExecutionContext workflowContext)
    {
        var values = ReadValues(workflowContext.Input.TryGetValue(SurveyWorkflowInput.ConditionValues, out var raw) ? raw : null);

        // A source the response does not know (a removed question) is left out, as the shared evaluator expects.
        return ConditionEvaluator.IsMet(Rule, source => values.TryGetValue(source, out var found) ? new ConditionValue(found) : null);
    }

    private static Dictionary<string, string[]> ReadValues(object raw)
    {
        var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        switch (raw)
        {
            case IReadOnlyDictionary<string, string[]> dictionary:
                foreach (var (key, value) in dictionary)
                {
                    values[key] = value ?? [];
                }

                break;
            case IDictionary<string, string[]> dictionary:
                foreach (var (key, value) in dictionary)
                {
                    values[key] = value ?? [];
                }

                break;
            case JsonObject json:
                foreach (var (key, node) in json)
                {
                    values[key] = node is JsonArray array ? array.Select(item => item?.ToString()).Where(item => item is not null).ToArray() : [];
                }

                break;
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                foreach (var property in element.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Select(item => item.ToString()).ToArray()
                        : [];
                }

                break;
        }

        return values;
    }
}
