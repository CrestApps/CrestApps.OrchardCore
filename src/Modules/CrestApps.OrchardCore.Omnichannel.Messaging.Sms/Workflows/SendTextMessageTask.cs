using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Workflows;

/// <summary>
/// A workflow task that sends a text from a chosen number of the business, such as the number an agent texts from that
/// the Find Agent Numbers task found. The text goes through the messaging workspace, so it is sent by the provider that
/// owns the number and appears in the customer's conversation, where a reply lands too.
/// </summary>
public sealed class SendTextMessageTask : TaskActivity<SendTextMessageTask>
{
    private readonly IMessagingConversationService _conversationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendTextMessageTask"/> class.
    /// </summary>
    /// <param name="conversationService">The messaging workspace's conversations, which send the text.</param>
    /// <param name="agentProfileManager">The agent profiles, to give a new conversation to the agent.</param>
    /// <param name="userManager">The user manager, to find the agent by name.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SendTextMessageTask(
        IMessagingConversationService conversationService,
        IAgentProfileManager agentProfileManager,
        UserManager<IUser> userManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<SendTextMessageTask> logger,
        IStringLocalizer<SendTextMessageTask> stringLocalizer)
    {
        _conversationService = conversationService;
        _agentProfileManager = agentProfileManager;
        _userManager = userManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Send Text Message"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the number to send from, one of the business's SMS numbers.
    /// </summary>
    public string From
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the customer's number.
    /// </summary>
    public string To
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid template of the message.
    /// </summary>
    public string Body
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the user name of the agent a new conversation belongs to.
    /// </summary>
    public string UserName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Done"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var from = await EvaluateAsync(From, workflowContext);
        var to = await EvaluateAsync(To, workflowContext);
        var body = await EvaluateAsync(Body, workflowContext);

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.IsNullOrEmpty(body))
        {
            _logger.LogWarning("The Send Text Message task needs a From number, a To number and a message; one of them resolved empty.");

            return Outcome("Failed");
        }

        var agentId = await ResolveAgentIdAsync(await EvaluateAsync(UserName, workflowContext));
        var result = await _conversationService.SendDirectAsync(OmnichannelConstants.Channels.Sms, from, to, body, agentId);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "The Send Text Message task could not send from {From} to {To}: {Error}",
                from.SanitizeLogValue(),
                to.SanitizeLogValue(),
                result.Error.SanitizeLogValue());

            return Outcome("Failed");
        }

        workflowContext.LastResult = result.Message?.Id;

        return Outcome("Done");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

    // The conversation a text starts belongs to the agent, so the customer's reply reaches them. Without one it is a
    // system send, like any other automated text.
    private async Task<string> ResolveAgentIdAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var user = await _userManager.FindByNameAsync(name) ?? await _userManager.FindByIdAsync(name);

        if (user is null)
        {
            return null;
        }

        var profile = await _agentProfileManager.FindByUserIdAsync(await _userManager.GetUserIdAsync(user));

        return profile?.ItemId;
    }
}
