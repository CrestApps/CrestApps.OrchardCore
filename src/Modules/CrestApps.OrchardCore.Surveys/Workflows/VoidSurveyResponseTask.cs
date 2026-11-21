using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Voids a survey response with a reason; it no longer counts in results and limits.
/// </summary>
public sealed class VoidSurveyResponseTask : SurveyTaskActivity<VoidSurveyResponseTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoidSurveyResponseTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public VoidSurveyResponseTask(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<VoidSurveyResponseTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Void Survey Response"];

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

        if (reason is null || _serviceProvider.GetService<ISurveyResponseService>() is not { } responses)
        {
            return "Failed";
        }

        var result = await responses.VoidAsync(responseId, SurveyWorkflowInput.WorkflowActor, reason);

        return ResponseTaskOutcome.From(result, responseId, output);
    }
}
