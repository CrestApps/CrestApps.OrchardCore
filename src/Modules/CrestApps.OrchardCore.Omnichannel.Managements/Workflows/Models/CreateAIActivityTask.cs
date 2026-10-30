using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;

/// <summary>
/// Creates an automated AI phone call or text for one customer: the AI profile answers, from one of your numbers,
/// with the variables the conversation starts with. The automated activities pass places it, after the same opt-out,
/// do-not-call and calling-window checks every automated activity goes through. Use it to start AI calls from
/// another system (an HTTP Request event) or for a list of customers (inside a For Each).
/// </summary>
public sealed class CreateAIActivityTask : TaskActivity<CreateAIActivityTask>
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContentManager _contentManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAIActivityTask"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="contentManager">The content manager, which loads the contact.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator, which renders the Liquid fields.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateAIActivityTask(
        IOmnichannelActivityManager activityManager,
        IContentManager contentManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IClock clock,
        ILogger<CreateAIActivityTask> logger,
        IStringLocalizer<CreateAIActivityTask> stringLocalizer)
    {
        _activityManager = activityManager;
        _contentManager = contentManager;
        _expressionEvaluator = expressionEvaluator;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Create AI Activity"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the channel: <see cref="OmnichannelConstants.Channels.Phone"/> or
    /// <see cref="OmnichannelConstants.Channels.Sms"/>.
    /// </summary>
    public string Channel
    {
        get => GetProperty(() => OmnichannelConstants.Channels.Phone);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the AI profile that holds the conversation.
    /// </summary>
    public string AIProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the number the call or text comes from.
    /// </summary>
    public string ChannelEndpointId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the customer's phone number.
    /// </summary>
    public string Destination
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the contact's content item id.
    /// </summary>
    public string ContactContentItemId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional subject content type the activity is about.
    /// </summary>
    public string SubjectContentType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional campaign the activity belongs to.
    /// </summary>
    public string CampaignId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the variables the conversation starts with, one <c>name: value</c> per line, as Liquid.
    /// </summary>
    public string Variables
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
        var destination = await EvaluateAsync(Destination, workflowContext);

        if (string.IsNullOrWhiteSpace(AIProfileId) || string.IsNullOrWhiteSpace(destination))
        {
            _logger.LogWarning("The Create AI Activity task needs an AI profile and a phone number; one of them is empty.");

            return Outcome("Failed");
        }

        var now = _clock.UtcNow;
        var channel = string.Equals(Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase)
            ? OmnichannelConstants.Channels.Sms
            : OmnichannelConstants.Channels.Phone;

        var activity = await _activityManager.NewAsync();
        activity.Kind = channel == OmnichannelConstants.Channels.Sms ? ActivityKind.Sms : ActivityKind.Call;
        activity.Source = ActivitySources.Workflow;
        activity.Channel = channel;
        activity.InteractionType = ActivityInteractionType.Automated;
        activity.AIProfileId = AIProfileId;
        activity.ChannelEndpointId = ChannelEndpointId;
        activity.PreferredDestination = destination;
        activity.CampaignId = string.IsNullOrWhiteSpace(CampaignId) ? null : CampaignId;
        activity.SubjectContentType = string.IsNullOrWhiteSpace(SubjectContentType) ? null : SubjectContentType;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(ActivityInteractionType.Automated, hasAssignedUser: false);
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;
        activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;

        var contactId = await EvaluateAsync(ContactContentItemId, workflowContext);

        if (!string.IsNullOrWhiteSpace(contactId))
        {
            var contact = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
                activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;
                activity.ContactResolvedUtc = now;
            }
        }

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        var variables = ParseVariables(await EvaluateAsync(Variables, workflowContext));

        if (variables.Count > 0)
        {
            activity.SetConversationVariables(variables);
        }

        await _activityManager.CreateAsync(activity);

        workflowContext.LastResult = activity.ItemId;
        workflowContext.Output["ActivityId"] = activity.ItemId;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The Create AI Activity task created automated {Channel} activity '{ActivityId}'.",
                channel,
                activity.ItemId.SanitizeLogValue());
        }

        return Outcome("Done");
    }

    /// <summary>
    /// Reads <c>name: value</c> lines into variables; lines without a name are skipped.
    /// </summary>
    /// <param name="text">The rendered text.</param>
    /// <returns>The variables.</returns>
    internal static List<KeyValuePair<string, string>> ParseVariables(string text)
    {
        var values = new List<KeyValuePair<string, string>>();

        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (name.Length > 0 && value.Length > 0)
            {
                values.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        return values;
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
