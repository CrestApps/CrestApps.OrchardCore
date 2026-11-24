using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Scores a survey response again with its version's key, such as after a key was fixed. It runs no score change check
/// (<see cref="ISurveyScoreChangeCheck"/>, such as Learning's Override grades): whoever may edit workflows configures it,
/// so that right should go only to people who may change grades.
/// </summary>
public sealed class RegradeSurveyResponseTask : SurveyTaskActivity<RegradeSurveyResponseTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegradeSurveyResponseTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public RegradeSurveyResponseTask(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<RegradeSurveyResponseTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Regrade Survey Response"];

    /// <inheritdoc />
    protected override IEnumerable<string> OutcomeNames => ["Done", "NotFound", "Failed"];

    /// <inheritdoc />
    protected override string DefaultOutputName => "SurveyResponse";

    /// <inheritdoc />
    protected override async Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
    {
        var responseId = await evaluate(ResponseId);
        var reason = await evaluate(Reason);

        if (responseId is null)
        {
            return "NotFound";
        }

        if (reason is null || _serviceProvider.GetService<ISurveyGradingService>() is not { } grading)
        {
            return "Failed";
        }

        var result = await grading.RegradeAsync(responseId, SurveyWorkflowInput.WorkflowActor, reason);

        return ResponseTaskOutcome.From(result, responseId, output);
    }
}
