using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// Acts on a phone call the AI is holding: gives the AI a note, hands the caller to an agent, transfers them to a
/// phone number, switches the AI profile, or ends the call. A workflow the AI calls as a tool can act on its own
/// call, and an <c>AI Conversation Turn</c> workflow on the call it was raised for.
/// </summary>
public sealed class AICallActionTask : TaskActivity<AICallActionTask>
{
    private readonly IEnumerable<IAIVoiceCallSupervision> _supervisions;
    private readonly IEnumerable<IAgentProfileManager> _agentProfileManagers;
    private readonly UserManager<IUser> _userManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICallActionTask"/> class.
    /// </summary>
    /// <param name="supervisions">The AI call supervision, when AI voice is on.</param>
    /// <param name="agentProfileManagers">
    /// The agent profiles, to find the agent a call is handed to. Empty without the Contact Center Agents feature: the
    /// task is registered whenever Orchard Core Workflows is on, and only handing the call to an agent needs them.
    /// </param>
    /// <param name="userManager">The user manager, to find the agent by user name.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICallActionTask(
        IEnumerable<IAIVoiceCallSupervision> supervisions,
        IEnumerable<IAgentProfileManager> agentProfileManagers,
        UserManager<IUser> userManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<AICallActionTask> logger,
        IStringLocalizer<AICallActionTask> stringLocalizer)
    {
        _supervisions = supervisions;
        _agentProfileManagers = agentProfileManagers;
        _userManager = userManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Call Action"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the call's activity.
    /// </summary>
    public string ActivityId
    {
        get => GetProperty(() => "{{ Workflow.Input.ActivityId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what to do, one of <see cref="AICallActions"/>.
    /// </summary>
    public string Action
    {
        get => GetProperty(() => AICallActions.Message);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression the action takes: the note, the agent's user name, the phone number, or
    /// the AI profile's identifier.
    /// </summary>
    public string Value
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var supervision = _supervisions.FirstOrDefault();
        var activityId = await EvaluateAsync(ActivityId, workflowContext);
        var value = await EvaluateAsync(Value, workflowContext);

        if (supervision is null || string.IsNullOrEmpty(activityId))
        {
            _logger.LogWarning("The AI Call Action task needs AI voice to be on and a call's activity.");

            return Outcome("Failed");
        }

        AIVoiceCallActionResult result = Action switch
        {
            AICallActions.Message => new(await supervision.SendNoteAsync(activityId, value), null),
            AICallActions.TakeOver => await supervision.TakeOverAsync(activityId, await ResolveAgentIdAsync(value), "Workflow"),
            AICallActions.TransferToNumber => await supervision.TransferToNumberAsync(activityId, value, "Workflow"),
            AICallActions.SwitchProfile => await supervision.SwitchProfileAsync(activityId, value),
            AICallActions.End => await supervision.EndAsync(activityId),
            _ => new(false, "Unknown action."),
        };

        workflowContext.LastResult = result.Message;

        // Said as a failure as well as routed to Failed: a workflow run as an AI tool reports its outputs to the model,
        // and without them a failed action read as done. Live, a refused profile switch reached the model as done, and
        // it told the caller they were being put through and then transferred them to a person instead.
        if (!result.Succeeded)
        {
            workflowContext.Output["status"] = "failed";
            workflowContext.Output["message"] = result.Message;
        }

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The AI Call Action task could not {Action} on activity '{ActivityId}': {Message}",
                Action,
                activityId.SanitizeLogValue(),
                result.Message.SanitizeLogValue());
        }

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> ResolveAgentIdAsync(string name)
    {
        var agentProfileManager = _agentProfileManagers.FirstOrDefault();

        if (string.IsNullOrEmpty(name) || agentProfileManager is null)
        {
            return null;
        }

        var user = await _userManager.FindByNameAsync(name) ?? await _userManager.FindByIdAsync(name);

        if (user is null)
        {
            return null;
        }

        var profile = await agentProfileManager.FindByUserIdAsync(await _userManager.GetUserIdAsync(user));

        return profile?.ItemId;
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
