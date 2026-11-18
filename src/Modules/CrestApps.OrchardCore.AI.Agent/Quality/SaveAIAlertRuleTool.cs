using System.Net.Mail;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Creates an AI alert rule or updates one, with the checks of the alert rule editor. A rule whose metric, condition,
/// threshold or profile changes is checked again at once, as the editor does.
/// </summary>
public sealed class SaveAIAlertRuleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIAlertRule";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "ruleId": {
          "type": "string",
          "description": "The ID of the rule to change, from listAIAlerts. Leave it out to create a rule."
        },
        "name": {
          "type": "string",
          "description": "The name of the rule. Required for a new rule."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the rule is checked. A new rule is on unless this is false."
        },
        "metric": {
          "type": "string",
          "enum": [{{string.Join(", ", Enum.GetNames<AIConversationMetric>().Select(name => $"\"{name}\""))}}],
          "description": "The conversation metric the rule watches. Rates (names ending in Rate) are percentages from 0 to 100."
        },
        "profileId": {
          "type": "string",
          "description": "Only conversations of this AI profile. An empty text watches every profile."
        },
        "condition": {
          "type": "string",
          "enum": [{{string.Join(", ", Enum.GetNames<AIConversationAlertCondition>().Select(name => $"\"{name}\""))}}],
          "description": "When the rule opens: the value is above or below the threshold, or rises or falls by more than the threshold in percent from the window before."
        },
        "threshold": {
          "type": "number",
          "description": "The threshold, in the metric's unit; for a rate, a percentage such as 20."
        },
        "windowMinutes": {
          "type": "integer",
          "minimum": 1,
          "maximum": 43200,
          "description": "The time window the metric is measured over, in minutes, up to 30 days. Defaults to 60."
        },
        "minimumSampleSize": {
          "type": "integer",
          "minimum": 0,
          "description": "How many conversations the window needs before the rule can open. Defaults to 10."
        },
        "checkEveryMinutes": {
          "type": "integer",
          "minimum": 1,
          "maximum": 1440,
          "description": "How often the rule is checked, in minutes, up to once a day. Defaults to 5."
        },
        "notifyEmails": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The email addresses told when the rule opens and closes. It replaces the stored list; an empty list emails nobody."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates an alert rule that watches a conversation metric over a time window and emails the listed addresses when it crosses a threshold. Can also turn a rule on or off.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIQuality))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var store = services.GetRequiredService<IAIAlertRuleStore>();
        arguments.TryGetFirstString("ruleId", out var ruleId);

        AIAlertRule rule;
        var isNew = string.IsNullOrWhiteSpace(ruleId);

        if (isNew)
        {
            rule = new AIAlertRule();
        }
        else
        {
            rule = await store.FindByIdAsync(ruleId.Trim(), cancellationToken);

            if (rule is null)
            {
                return $"Nothing was saved: no alert rule has the ID '{ruleId}'. Use listAIAlerts to find it.";
            }
        }

        var watched = AIAlertRuleValidator.GetWatched(rule);
        var refusal = Apply(arguments, rule);

        if (refusal is not null)
        {
            return $"Nothing was saved: {refusal}";
        }

        var errors = AIAlertRuleValidator.Validate(rule, services.GetRequiredService<IStringLocalizer<SaveAIAlertRuleTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {QualityToolSupport.Describe(errors)}";
        }

        if (rule.ProfileId is not null && await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(rule.ProfileId, cancellationToken) is null)
        {
            return $"Nothing was saved: no AI profile has the ID '{rule.ProfileId}'. Use listAIProfiles to find it, or leave profileId empty to watch every profile.";
        }

        // A changed rule is checked at once.
        AIAlertRuleValidator.ResetCheckWhenWatchChanged(rule, watched);

        if (isNew)
        {
            rule.ItemId = UniqueId.GenerateId();
            rule.CreatedUtc = services.GetRequiredService<IClock>().UtcNow;

            await store.CreateAsync(rule, cancellationToken);
        }
        else
        {
            await store.UpdateAsync(rule, cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
            id = rule.ItemId,
            name = rule.Name,
            enabled = rule.Enabled,
            metric = rule.Metric.ToString(),
            profileId = rule.ProfileId,
            condition = rule.Condition.ToString(),
            threshold = rule.Threshold,
            windowMinutes = rule.WindowMinutes,
            minimumSampleSize = rule.MinimumSampleSize,
            checkEveryMinutes = rule.CheckEveryMinutes,
            notifyEmails = SplitEmails(rule.NotifyEmails),
            note = rule.Enabled ? "The rule is checked within a minute." : "The rule is off and is not checked.",
            editPath = QualityToolSupport.GetAlertRuleEditPath(services, rule.ItemId),
        });
    }

    private static string Apply(AIFunctionArguments arguments, AIAlertRule rule)
    {
        if (QualityToolSupport.TryReadText(arguments, "name", out var name))
        {
            rule.Name = name;
        }

        if (QualityToolSupport.ReadBool(arguments, "enabled") is { } enabled)
        {
            rule.Enabled = enabled;
        }

        if (arguments.TryGetFirstString("metric", out var metricText))
        {
            if (!Enum.TryParse<AIConversationMetric>(metricText.Trim(), ignoreCase: true, out var metric) || !Enum.IsDefined(metric))
            {
                return $"metric must be one of {string.Join(", ", Enum.GetNames<AIConversationMetric>())}.";
            }

            rule.Metric = metric;
        }

        if (QualityToolSupport.TryReadText(arguments, "profileId", out var profileId))
        {
            rule.ProfileId = profileId;
        }

        if (arguments.TryGetFirstString("condition", out var conditionText))
        {
            if (!Enum.TryParse<AIConversationAlertCondition>(conditionText.Trim(), ignoreCase: true, out var condition) || !Enum.IsDefined(condition))
            {
                return $"condition must be one of {string.Join(", ", Enum.GetNames<AIConversationAlertCondition>())}.";
            }

            rule.Condition = condition;
        }

        if (!QualityToolSupport.TryReadDouble(arguments, "threshold", out var threshold))
        {
            return "threshold must be a number.";
        }

        if (threshold is not null)
        {
            rule.Threshold = threshold.Value;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "windowMinutes", out var windowMinutes))
        {
            return "windowMinutes must be a whole number.";
        }

        if (windowMinutes is not null)
        {
            rule.WindowMinutes = windowMinutes.Value;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "minimumSampleSize", out var minimumSampleSize))
        {
            return "minimumSampleSize must be a whole number.";
        }

        if (minimumSampleSize is not null)
        {
            rule.MinimumSampleSize = minimumSampleSize.Value;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "checkEveryMinutes", out var checkEveryMinutes))
        {
            return "checkEveryMinutes must be a whole number.";
        }

        if (checkEveryMinutes is not null)
        {
            rule.CheckEveryMinutes = checkEveryMinutes.Value;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "notifyEmails", out var emails))
        {
            return "notifyEmails must be a list of email addresses.";
        }

        if (emails is not null)
        {
            var invalid = emails.Where(email => !MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)).ToList();

            if (invalid.Count > 0)
            {
                return $"{string.Join(", ", invalid.Select(email => $"'{email}'"))} {(invalid.Count == 1 ? "is not an email address" : "are not email addresses")}.";
            }

            rule.NotifyEmails = emails.Count == 0 ? null : string.Join(", ", emails);
        }

        return null;
    }

    private static List<string> SplitEmails(string emails)
        => (emails ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
