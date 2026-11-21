using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Closes a survey, so it collects no more responses.
/// </summary>
public sealed class CloseSurveyTask : SurveyTaskActivity<CloseSurveyTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloseSurveyTask"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services.</param>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CloseSurveyTask(
        IServiceProvider serviceProvider,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<CloseSurveyTask> stringLocalizer)
        : base(expressionEvaluator, stringLocalizer)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Close Survey"];

    /// <inheritdoc />
    protected override Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output)
        => SurveyStatusTask.ChangeAsync(_serviceProvider, evaluate, SurveyId, SurveyStatus.Closed, output);
}
