using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Tests an AI profile against a customer played by a model, then scores the conversation against criteria. Run a
/// batch with <c>For Each</c>, before a change goes live or on a schedule.
/// </summary>
public sealed class SimulateConversationTask : TaskActivity<SimulateConversationTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IAIProfileManager _profileManager;
    private readonly AIConversationSimulator _simulator;
    private readonly AIConversationEvaluationService _evaluationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulateConversationTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="simulator">The simulator that plays the conversation.</param>
    /// <param name="evaluationService">The service that scores and keeps the result.</param>
    /// <param name="serviceProvider">The service provider, which gives the workflow manager the evaluated event is raised with.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SimulateConversationTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAIProfileManager profileManager,
        AIConversationSimulator simulator,
        AIConversationEvaluationService evaluationService,
        IServiceProvider serviceProvider,
        ILogger<SimulateConversationTask> logger,
        IStringLocalizer<SimulateConversationTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _profileManager = profileManager;
        _simulator = simulator;
        _evaluationService = evaluationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Simulate AI Conversation"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the AI profile under test.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets who the simulated customer is, what they want and how they behave, as a Liquid template.
    /// </summary>
    public string Scenario
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the conversation variables the test starts with, one <c>name: value</c> per line.
    /// </summary>
    public string Variables
    {
        get => GetProperty<string>();
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
    /// Gets or sets the most customer turns.
    /// </summary>
    public int MaxTurns
    {
        get => GetProperty(() => AIConversationSimulator.DefaultMaxTurns);
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
    /// Gets or sets a Liquid expression for the label the result is kept under, such as the test suite's name.
    /// </summary>
    public string Label
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Passed"]), new Outcome(S["Failed"]), new Outcome(S["Error"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var profile = string.IsNullOrWhiteSpace(ProfileId) ? null : await _profileManager.FindByIdAsync(ProfileId);

        if (profile is null)
        {
            workflowContext.Output["SimulationError"] = "The AI profile was not found.";

            return Outcome("Error");
        }

        var scenario = await RenderAsync(Scenario, workflowContext);
        var criteria = AIConversationEvaluator.ParseCriteria(await RenderAsync(Criteria, workflowContext));
        var label = await RenderAsync(Label, workflowContext);
        var variables = SetConversationVariablesTask.ParseLines(await RenderAsync(Variables, workflowContext))
            .Where(variable => variable.Value is not null)
            .GroupBy(variable => variable.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        try
        {
            var simulation = await _simulator.SimulateAsync(new AIConversationSimulationRequest
            {
                Profile = profile,
                Scenario = scenario,
                Variables = variables,
                MaxTurns = MaxTurns,
            });

            workflowContext.Output["Transcript"] = AIConversationTranscript.Format(simulation.Turns);
            workflowContext.Output["Turns"] = simulation.Turns;

            var evaluation = await _evaluationService.EvaluateTurnsAsync(profile, null, simulation.Turns, criteria, PassingScore, label, simulation.Instructions);

            if (evaluation is null)
            {
                workflowContext.Output["SimulationError"] = "The conversation could not be scored: no deployment can judge it, or the customer never spoke.";

                return Outcome("Error");
            }

            workflowContext.Output["Evaluation"] = evaluation;
            workflowContext.LastResult = evaluation;

            await AIConversationEvaluatedEvent.TriggerAsync(_serviceProvider, evaluation, _logger);

            return Outcome(evaluation.Passed ? "Passed" : "Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulating a conversation with AI profile '{ProfileId}' failed.", profile.ItemId);
            workflowContext.Output["SimulationError"] = ex.Message;

            return Outcome("Error");
        }
    }

    private async Task<string> RenderAsync(string template, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(template)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(template), workflowContext, null);
}
