using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.UrlRewriting;
using OrchardCore.UrlRewriting.Models;
using OrchardCore.UrlRewriting.Services;

namespace CrestApps.OrchardCore.AI.Agent.UrlRewriting;

/// <summary>
/// Creates or updates one URL rewriting rule through the rule manager, as the UrlRewriting recipe step does: the rule
/// handlers fill the rule from the given values, the manager validates it, and the rule is saved only when it is valid.
/// The tenant is then reloaded, as the URL Rewriting page does, so live requests use the new rules.
/// </summary>
public sealed class SaveUrlRewriteRuleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveUrlRewriteRule";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the rule to change, from listUrlRewriteRules. Leave it out to create a rule. Fields left out of a change keep their value."
        },
        "type": {
          "type": "string",
          "enum": [
            "Redirect",
            "Rewrite"
          ],
          "description": "Required for a new rule. Redirect sends the browser to the new address with a redirect status; Rewrite serves the new address under the requested one. The type of an existing rule cannot change."
        },
        "name": {
          "type": "string",
          "description": "The name of the rule, shown on the URL Rewriting page. Required for a new rule."
        },
        "pattern": {
          "type": "string",
          "description": "The regular expression the request path is matched against, in Apache mod_rewrite syntax and without the leading slash, for example ^old-blog/(.*)$. Required for a new rule."
        },
        "substitutionPattern": {
          "type": "string",
          "description": "The address the request goes to, which can use the groups of the pattern, for example /blog/$1. Required for a new rule."
        },
        "isCaseInsensitive": {
          "type": "boolean",
          "description": "Whether the pattern ignores letter case."
        },
        "queryStringPolicy": {
          "type": "string",
          "enum": [
            "Append",
            "Drop"
          ],
          "description": "Whether the query string of the request is added to the new address or dropped."
        },
        "redirectType": {
          "type": "string",
          "enum": [
            "Found",
            "MovedPermanently",
            "TemporaryRedirect",
            "PermanentRedirect"
          ],
          "description": "Redirect rules only: the status sent, Found (302), MovedPermanently (301), TemporaryRedirect (307) or PermanentRedirect (308). A new redirect uses Found when it is left out."
        },
        "skipFurtherRules": {
          "type": "boolean",
          "description": "Rewrite rules only: whether the rules after this one are skipped when it matches."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates one redirect or rewrite rule (pattern, substitution and options); a new rule is applied after the existing ones. Rules apply to live requests at once; a wrong pattern can make site pages unreachable.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UrlRewritingPermissions.ManageUrlRewritingRules))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("id", out var id);
        arguments.TryGetFirstString("type", out var type);
        id = id?.Trim();
        type = type?.Trim();

        if (!TryBuildData(arguments, out var data, out var error))
        {
            return error;
        }

        var rulesManager = services.GetRequiredService<IRewriteRulesManager>();
        var changes = UrlRewriteRuleScopeChanges.Get(services);
        RewriteRule rule;
        bool created;

        if (!string.IsNullOrEmpty(id))
        {
            var stored = await changes.FindAsync(rulesManager, id);

            if (stored is null)
            {
                return $"Nothing was saved: there is no URL rewriting rule with the ID '{id}'. Use listUrlRewriteRules to find it, or leave the ID out to create a rule.";
            }

            if (!string.IsNullOrEmpty(type) && !string.Equals(type, stored.Source, StringComparison.Ordinal))
            {
                return $"Nothing was saved: the rule '{stored.Name}' is a {stored.Source} rule and its type cannot change. Delete it and create a {type} rule instead.";
            }

            if (!TryCheckOptions(stored.Source, data, out error))
            {
                return error;
            }

            rule = UrlRewriteRuleToolSupport.Copy(stored);
            await rulesManager.UpdateAsync(rule, data);
            created = false;
        }
        else
        {
            if (type is not (UrlRedirectRuleSource.SourceName or UrlRewriteRuleSource.SourceName))
            {
                return "Nothing was saved: give the type of the new rule, Redirect or Rewrite.";
            }

            if (!TryCheckOptions(type, data, out error))
            {
                return error;
            }

            rule = await rulesManager.NewAsync(type, data);

            if (rule is null)
            {
                return $"Nothing was saved: {type} rules are not available on this site.";
            }

            created = true;
        }

        var validation = await rulesManager.ValidateAsync(rule);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(validationError => validationError.ErrorMessage));
        }

        await rulesManager.SaveAsync(rule);
        changes.Saved(rule);
        UrlRewriteRuleToolSupport.RequestRelease(services);

        var localClock = services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            created,
            rule = UrlRewriteRuleToolSupport.Describe(rule, rule.Order + 1, await AgentToolResults.ToLocalAsync(localClock, rule.CreatedUtc)),
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.UrlRewriting", "Admin", "Edit", new() { ["id"] = rule.Id }),
            note = "The site reloads its rules once this request ends; pages under the admin prefix are never rewritten. Change the order by dragging the rules on the URL Rewriting page.",
        });
    }

    // The rule handlers read the values by these names; only values the caller gave are passed, so the others keep
    // their value.
    private static bool TryBuildData(AIFunctionArguments arguments, out JsonObject data, out string error)
    {
        data = [];
        error = null;

        foreach (var (argument, property) in new[]
        {
            ("name", nameof(RewriteRule.Name)),
            ("pattern", nameof(UrlRedirectSourceMetadata.Pattern)),
            ("substitutionPattern", nameof(UrlRedirectSourceMetadata.SubstitutionPattern)),
        })
        {
            if (arguments.TryGetFirstString(argument, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                data[property] = value.Trim();
            }
        }

        foreach (var (argument, property) in new[]
        {
            ("isCaseInsensitive", nameof(UrlRedirectSourceMetadata.IsCaseInsensitive)),
            ("skipFurtherRules", nameof(UrlRewriteSourceMetadata.SkipFurtherRules)),
        })
        {
            if (arguments.TryGetFirst(argument, out _))
            {
                if (!arguments.TryGetFirst<bool>(argument, out var flag))
                {
                    error = $"Nothing was saved: {argument} must be true or false.";

                    return false;
                }

                data[property] = flag;
            }
        }

        if (arguments.TryGetFirstString("queryStringPolicy", out var policy) && !string.IsNullOrWhiteSpace(policy))
        {
            if (!Enum.TryParse<QueryStringPolicy>(policy.Trim(), ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = $"Nothing was saved: the query string policy '{policy}' is not known. Use Append or Drop.";

                return false;
            }

            data[nameof(UrlRedirectSourceMetadata.QueryStringPolicy)] = parsed.ToString();
        }

        if (arguments.TryGetFirstString("redirectType", out var redirectType) && !string.IsNullOrWhiteSpace(redirectType))
        {
            // A status code such as 301 is accepted too.
            if (!Enum.TryParse<RedirectType>(redirectType.Trim(), ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = $"Nothing was saved: the redirect type '{redirectType}' is not known. Use Found, MovedPermanently, TemporaryRedirect or PermanentRedirect.";

                return false;
            }

            data[nameof(UrlRedirectSourceMetadata.RedirectType)] = parsed.ToString();
        }

        return true;
    }

    private static bool TryCheckOptions(string source, JsonObject data, out string error)
    {
        error = null;

        if (source == UrlRewriteRuleSource.SourceName && data.ContainsKey(nameof(UrlRedirectSourceMetadata.RedirectType)))
        {
            error = "Nothing was saved: redirectType applies to Redirect rules only.";

            return false;
        }

        if (source == UrlRedirectRuleSource.SourceName && data.ContainsKey(nameof(UrlRewriteSourceMetadata.SkipFurtherRules)))
        {
            error = "Nothing was saved: skipFurtherRules applies to Rewrite rules only.";

            return false;
        }

        return true;
    }
}
