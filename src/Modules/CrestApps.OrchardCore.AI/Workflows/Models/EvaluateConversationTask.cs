using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Scores a finished AI conversation against criteria with the utility model, keeps the evaluation for QA reports
/// and alerts, and goes on by whether it passed.
/// </summary>
public sealed class EvaluateConversationTask : TaskActivity<EvaluateConversationTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly AIConversationEvaluationService _evaluationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateConversationTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="evaluationService">The service that evaluates and keeps the result.</param>
    /// <param name="serviceProvider">The service provider, which gives the workflow manager the evaluated event is raised with.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EvaluateConversationTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        AIConversationEvaluationService evaluationService,
        IServiceProvider serviceProvider,
        ILogger<EvaluateConversationTask> logger,
        IStringLocalizer<EvaluateConversationTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _evaluationService = evaluationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Evaluate AI Conversation"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets a Liquid expression for the AI session to evaluate.
    /// </summary>
    public string SessionId
    {
        get => GetProperty(() => "{{ Workflow.Input.SessionId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the criteria, one per line, rendered as a Liquid template.
    /// </summary>
    public string Criteria
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the lowest score that passes.
    /// </summary>
    public int PassingScore
    {
        get => GetProperty(() => AIConversationEvaluator.DefaultPassingScore);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the label the evaluation is kept under, such as a cohort or a variant.
    /// </summary>
    public string Label
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Passed"]), new Outcome(S["Failed"]), new Outcome(S["Not evaluated"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var sessionId = await RenderAsync(SessionId, workflowContext);
        var criteria = AIConversationEvaluator.ParseCriteria(await RenderAsync(Criteria, workflowContext));
        var label = await RenderAsync(Label, workflowContext);

        AIConversationEvaluation evaluation;

        try
        {
            evaluation = await _evaluationService.EvaluateSessionAsync(sessionId, criteria, PassingScore, label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluating AI session '{SessionId}' failed.", sessionId);

            return Outcome("Not evaluated");
        }

        if (evaluation is null)
        {
            return Outcome("Not evaluated");
        }

        workflowContext.Output["Evaluation"] = evaluation;
        workflowContext.LastResult = evaluation;

        await AIConversationEvaluatedEvent.TriggerAsync(_serviceProvider, evaluation, _logger);

        return Outcome(evaluation.Passed ? "Passed" : "Failed");
    }

    private async Task<string> RenderAsync(string template, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(template)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(template), workflowContext, null);
}
