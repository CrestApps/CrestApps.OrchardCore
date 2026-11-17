using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.UrlRewriting;
using OrchardCore.UrlRewriting.Models;

namespace CrestApps.OrchardCore.AI.Agent.UrlRewriting;

/// <summary>
/// Deletes URL rewriting rules through the rule manager, as the Delete action and the bulk Delete of the URL
/// Rewriting page do, and reloads the tenant so live requests stop using them.
/// </summary>
public sealed class DeleteUrlRewriteRuleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteUrlRewriteRule";

    private const int MaxRules = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "ids": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "items": {
            "type": "string"
          },
          "description": "The IDs of the rules to delete, from listUrlRewriteRules."
        }
      },
      "required": [
        "ids"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one or more URL rewriting rules by id.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, UrlRewritingPermissions.ManageUrlRewritingRules))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "ids", out var ids) || ids is null || ids.Count == 0)
        {
            return "Nothing was deleted: give the IDs of the rules, from listUrlRewriteRules.";
        }

        if (ids.Count > MaxRules)
        {
            return $"Nothing was deleted: give at most {MaxRules} rules at a time.";
        }

        var rulesManager = services.GetRequiredService<IRewriteRulesManager>();
        var rules = new List<RewriteRule>();
        var unknown = new List<string>();

        foreach (var id in ids)
        {
            var rule = await rulesManager.FindByIdAsync(id);

            if (rule is null)
            {
                unknown.Add(id);
            }
            else
            {
                rules.Add(rule);
            }
        }

        // Delete all or nothing, so a mistyped ID does not leave half of the change done.
        if (unknown.Count > 0)
        {
            return $"Nothing was deleted: there is no URL rewriting rule with the ID {string.Join(", ", unknown.Select(id => $"'{id}'"))}. Use listUrlRewriteRules to find the IDs.";
        }

        foreach (var rule in rules)
        {
            await rulesManager.DeleteAsync(rule);
        }

        UrlRewriteRuleToolSupport.RequestRelease(services);

        return AgentToolResults.Json(new
        {
            deleted = rules
                .Select(rule => new
                {
                    id = rule.Id,
                    name = rule.Name,
                    type = rule.Source,
                })
                .ToList(),
            note = "The rules cannot be restored. The site stops applying them once this request ends.",
        });
    }
}
