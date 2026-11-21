using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Writes a value into the properties of a survey response, for later steps and reports.
/// </summary>
public sealed class SetSurveyResponsePropertyTask : SurveyTaskActivity<SetSurveyResponsePropertyTask>
{
    private readonly SurveyResponseStore _responseStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSurveyResponsePropertyTask"/> class.
    /// </summary>
    /// <param name="responseStore">The response store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SetSurveyResponsePropertyTask(
        SurveyResponseStore responseStore,
        IClock clock,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<SetSurveyResponsePropertyTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _responseStore = responseStore;
        _clock = clock;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Set Survey Response Property"];

    /// <inheritdoc />
    protected override IEnumerable<string> OutcomeNames => ["Done", "NotFound", "Failed"];

    /// <inheritdoc />
    protected override string DefaultOutputName => "SurveyResponse";

    /// <inheritdoc />
    protected override async Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
    {
        var responseId = await evaluate(ResponseId);
        var name = await evaluate(PropertyName);

        if (responseId is null || await _responseStore.FindByIdAsync(responseId) is not { } response)
        {
            return "NotFound";
        }

        if (name is null)
        {
            return "Failed";
        }

        var value = await evaluate(PropertyValue);

        response.Properties[name] = value;
        response.ModifiedUtc = _clock.UtcNow;
        await _responseStore.UpdateAsync(response);

        output["ResponseId"] = responseId;
        output["Name"] = name;
        output["Value"] = value;

        return "Done";
    }
}
