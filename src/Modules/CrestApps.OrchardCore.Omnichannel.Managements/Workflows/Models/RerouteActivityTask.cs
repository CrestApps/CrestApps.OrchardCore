using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;

/// <summary>
/// A workflow task that decides who makes the next call: it moves an activity to an agent or to the dialer, or to
/// another campaign or dialer profile. A finished activity's open follow-ups are moved instead, and when it has none
/// its next attempt can be created already moved.
/// </summary>
/// <remarks>
/// A dialer record whose disposition says "Try again" is queued for the dialer again, in the same campaign, under the
/// same profile. A workflow reacting to <c>ActivityDispositionApplied</c> can now give that next call to an agent
/// instead -- the dialer then no longer calls it -- move it to another campaign or profile, or put manual work on the
/// dialer.
/// </remarks>
public sealed class RerouteActivityTask : TaskActivity<RerouteActivityTask>
{
    /// <summary>
    /// The default expression that resolves the activity: the one the Contact Center event is about.
    /// </summary>
    public const string DefaultActivityExpression = "{{ Workflow.Input.Data.ActivityItemId }}";

    private readonly ActivityRerouter _rerouter;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RerouteActivityTask"/> class.
    /// </summary>
    /// <param name="rerouter">The service that moves the activity.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RerouteActivityTask(
        ActivityRerouter rerouter,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<RerouteActivityTask> logger,
        IStringLocalizer<RerouteActivityTask> stringLocalizer)
    {
        _rerouter = rerouter;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Reroute Activity"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the activity to reroute.
    /// </summary>
    public string ActivityItemId
    {
        get => GetProperty(() => DefaultActivityExpression);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets who works the activity next.
    /// </summary>
    public ActivityRerouteTarget Target
    {
        get => GetProperty(() => ActivityRerouteTarget.Agent);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the campaign the activity moves to. Empty keeps its campaign.
    /// </summary>
    public string CampaignId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the dialer profile that dials the activity, when it goes to the dialer.
    /// </summary>
    public string DialerProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the source of the activity when an agent works it. Empty is <c>Manual</c>.
    /// </summary>
    public string Source
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the user name of the agent who works the activity. Empty keeps
    /// the owner it has.
    /// </summary>
    public string UserName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether a finished activity with no open follow-up gets its next attempt created, already rerouted.
    /// </summary>
    public bool CreateNextAttempt
    {
        get => GetProperty(() => true);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves how many minutes from now a created next attempt is due. Empty
    /// is due now.
    /// </summary>
    public string DelayMinutes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Rerouted"]),
            new Outcome(S["Nothing To Reroute"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activityItemId = await EvaluateAsync(ActivityItemId, workflowContext);
        int? delayMinutes = null;
        var delayText = await EvaluateAsync(DelayMinutes, workflowContext);

        if (!string.IsNullOrEmpty(delayText))
        {
            if (!int.TryParse(delayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 0)
            {
                _logger.LogWarning(
                    "The Reroute Activity task resolved a delay of '{Delay}', which is not a whole number of minutes.",
                    delayText.SanitizeLogValue());

                return Outcome("Failed");
            }

            delayMinutes = minutes;
        }

        try
        {
            var result = await _rerouter.RerouteAsync(activityItemId, new ActivityRerouteRequest
            {
                Target = Target,
                CampaignId = CampaignId,
                DialerProfileId = DialerProfileId,
                Source = Source,
                AssignToUser = await EvaluateAsync(UserName, workflowContext),
                CreateNextAttempt = CreateNextAttempt,
                DelayMinutes = delayMinutes,
            });

            workflowContext.Output["ReroutedActivityItemIds"] = result.ActivityItemIds.ToArray();
            workflowContext.Output["CreatedNextAttempt"] = result.CreatedNextAttempt;
            workflowContext.LastResult = result.ActivityItemIds.FirstOrDefault();

            switch (result.Status)
            {
                case ActivityRerouteStatus.Rerouted:
                    return Outcome("Rerouted");

                case ActivityRerouteStatus.NothingToReroute:
                    return Outcome("Nothing To Reroute");

                default:
                    _logger.LogWarning(
                        "The Reroute Activity task could not reroute activity '{ActivityItemId}': {Reason}",
                        activityItemId.SanitizeLogValue(),
                        result.Reason.SanitizeLogValue());

                    return Outcome("Failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while rerouting activity '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

            return Outcome("Failed");
        }
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
