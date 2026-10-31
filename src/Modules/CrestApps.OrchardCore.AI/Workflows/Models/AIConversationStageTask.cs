using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// A stage of a conversation flow. When the workflow reaches it, the conversation's AI gets the stage's instructions and
/// its ways out, and the workflow waits. When the AI decides one of the ways out applies, the workflow goes on by that
/// outcome to the next stage, or to any other activity: a condition, a script, an HTTP request, a text, a transfer.
/// </summary>
public sealed class AIConversationStageTask : TaskActivity<AIConversationStageTask>
{
    /// <summary>
    /// The workflow input that names the outcome the stage is left by.
    /// </summary>
    public const string OutcomeInput = "StageOutcome";

    private readonly IAIChatSessionManager _sessionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationStageTask"/> class.
    /// </summary>
    /// <param name="sessionManager">The chat session manager, where the stage is recorded.</param>
    /// <param name="liquidTemplateManager">Renders the instructions.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationStageTask(
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<AIConversationStageTask> stringLocalizer)
    {
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Conversation Stage"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the stage's name, shown in the conversation's history.
    /// </summary>
    public string StageName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid template of what the AI does in this stage.
    /// </summary>
    public string Instructions
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the ways out, one <c>outcome: when to take it</c> per line.
    /// </summary>
    public string WaysOut
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => AIConversationFlowPrompt.ParseOutcomes(WaysOut).Select(outcome => new Outcome(new LocalizedString(outcome.Name, outcome.Name)))
            .Append(new Outcome(S["Not in a conversation"]));

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var session = string.IsNullOrEmpty(workflowContext.CorrelationId) ? null : await _sessionManager.FindByIdAsync(workflowContext.CorrelationId);

        if (session is null)
        {
            return Outcome("Not in a conversation");
        }

        var instructions = string.IsNullOrWhiteSpace(Instructions)
            ? null
            : await _liquidTemplateManager.RenderStringAsync(Instructions, NullEncoder.Default, new Dictionary<string, FluidValue>
            {
                ["Workflow"] = new ObjectValue(workflowContext),
            });

        var state = session.GetOrCreate<AIConversationFlowState>();
        var now = _clock.UtcNow;
        var name = string.IsNullOrWhiteSpace(StageName) ? activityContext.ActivityRecord.ActivityId : StageName.Trim();

        state.WorkflowId = workflowContext.WorkflowId;
        state.StageActivityId = activityContext.ActivityRecord.ActivityId;
        state.StageName = name;
        state.Instructions = instructions?.Trim();
        state.Outcomes = AIConversationFlowPrompt.ParseOutcomes(WaysOut);
        state.Finished = false;
        state.EnteredUtc = now;
        state.History.Add(new AIConversationFlowStep { StageName = name, EnteredUtc = now });

        session.Put(state);
        await _sessionManager.SaveAsync(session);

        return Halt();
    }

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var outcome = workflowContext.Input.TryGetValue(OutcomeInput, out var value) ? value?.ToString() : null;

        return string.IsNullOrEmpty(outcome)
            ? Halt()
            : Outcome(outcome);
    }
}
