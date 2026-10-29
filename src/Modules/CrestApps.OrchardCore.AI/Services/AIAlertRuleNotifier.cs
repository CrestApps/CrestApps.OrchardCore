using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Tells people and systems that an alert rule triggered or resolved: by email, by the alert webhook events, and by
/// the <c>AI Alert Triggered</c> and <c>AI Alert Resolved</c> workflow events.
/// </summary>
public sealed class AIAlertRuleNotifier
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRuleNotifier"/> class.
    /// </summary>
    /// <param name="serviceProvider">The request's services, for the optional email and workflow services.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIAlertRuleNotifier(IServiceProvider serviceProvider, ILogger<AIAlertRuleNotifier> logger, IStringLocalizer<AIAlertRuleNotifier> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Notifies a change.
    /// </summary>
    /// <param name="rule">The rule, after the check.</param>
    /// <param name="change">What changed.</param>
    /// <returns>A task that completes when everyone was told.</returns>
    public async Task NotifyAsync(AIAlertRule rule, AIAlertChange change)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (change == AIAlertChange.None)
        {
            return;
        }

        var triggered = change == AIAlertChange.Triggered;
        var data = new Dictionary<string, object>
        {
            ["RuleId"] = rule.ItemId,
            ["RuleName"] = rule.Name,
            ["Metric"] = rule.Metric.ToString(),
            ["Condition"] = rule.Condition.ToString(),
            ["Threshold"] = rule.Threshold,
            ["Value"] = rule.LastValue,
            ["SampleSize"] = rule.LastSampleSize,
            ["WindowMinutes"] = rule.WindowMinutes,
            ["ProfileId"] = rule.ProfileId,
            ["Status"] = triggered ? "Triggered" : "Resolved",
        };

        await AIWebhookPublisher.TryPublishAsync(_serviceProvider, triggered ? AIWebhookEvents.AlertTriggered : AIWebhookEvents.AlertResolved, data);
        await EmailAsync(rule, triggered);

        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager is null)
        {
            return;
        }

        try
        {
            await workflowManager.TriggerEventAsync(triggered ? "AIAlertTriggeredEvent" : "AIAlertResolvedEvent", data, correlationId: rule.ItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The workflow event of alert rule '{RuleId}' could not be raised.", rule.ItemId.SanitizeLogValue());
        }
    }

    private async Task EmailAsync(AIAlertRule rule, bool triggered)
    {
        var recipients = (rule.NotifyEmails ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (recipients.Length == 0 || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        var subject = triggered
            ? S["AI alert: {0}", rule.Name].Value
            : S["Resolved: {0}", rule.Name].Value;
        var body = triggered
            ? S["The AI alert rule '{0}' crossed its threshold: {1} is {2} ({3} {4}, over the last {5} minutes, {6} samples).", rule.Name, rule.Metric, rule.LastValue, rule.Condition, rule.Threshold, rule.WindowMinutes, rule.LastSampleSize].Value
            : S["The AI alert rule '{0}' is back within its threshold: {1} is {2}.", rule.Name, rule.Metric, rule.LastValue].Value;

        try
        {
            var result = await emailService.SendAsync(string.Join(',', recipients), subject, body);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The email of alert rule '{RuleId}' was not sent.", rule.ItemId.SanitizeLogValue());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The email of alert rule '{RuleId}' could not be sent.", rule.ItemId.SanitizeLogValue());
        }
    }
}
