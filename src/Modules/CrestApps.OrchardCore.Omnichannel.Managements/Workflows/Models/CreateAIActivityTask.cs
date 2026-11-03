using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly AIActivityStarter _starter;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAIActivityTask"/> class.
    /// </summary>
    /// <param name="starter">Creates the automated activity.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator, which renders the Liquid fields.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateAIActivityTask(
        AIActivityStarter starter,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<CreateAIActivityTask> logger,
        IStringLocalizer<CreateAIActivityTask> stringLocalizer)
    {
        _starter = starter;
        _expressionEvaluator = expressionEvaluator;
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

        var activity = await _starter.StartAsync(new AIActivityStartRequest
        {
            Channel = Channel,
            AIProfileId = AIProfileId,
            Destination = destination,
            ChannelEndpointId = ChannelEndpointId,
            CampaignId = CampaignId,
            SubjectContentType = SubjectContentType,
            ContactContentItemId = await EvaluateAsync(ContactContentItemId, workflowContext),
            Variables = ParseVariables(await EvaluateAsync(Variables, workflowContext)),
            Source = ActivitySources.Workflow,
        });

        workflowContext.LastResult = activity.ItemId;
        workflowContext.Output["ActivityId"] = activity.ItemId;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The Create AI Activity task created automated {Channel} activity '{ActivityId}'.",
                activity.Channel,
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
