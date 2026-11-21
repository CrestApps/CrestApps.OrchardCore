using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Opens a published survey again, so it collects responses.
/// </summary>
public sealed class OpenSurveyTask : SurveyTaskActivity<OpenSurveyTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSurveyTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public OpenSurveyTask(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<OpenSurveyTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Open Survey"];

    /// <inheritdoc />
    protected override Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
        => SurveyStatusTask.ChangeAsync(_serviceProvider, evaluate, SurveyId, SurveyStatus.Open, output);
}
