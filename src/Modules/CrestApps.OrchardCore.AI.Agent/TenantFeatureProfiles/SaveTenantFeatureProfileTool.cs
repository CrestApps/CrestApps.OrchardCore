using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;

/// <summary>
/// Creates or replaces a tenant feature profile through the <c>FeatureProfiles</c> recipe step, which saves it with
/// the service the Feature Profiles page uses. As that page does, the profile name must be unique.
/// </summary>
public sealed class SaveTenantFeatureProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveTenantFeatureProfile";

    private const string NameMember = "Name";

    private static readonly string[] _ruleNames = ["Include", "Exclude"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the profile to replace, from listTenantFeatureProfiles. Leave it out to create a new profile."
        },
        "name": {
          "type": "string",
          "description": "The name of the profile. No other profile may have it."
        },
        "rules": {
          "type": "array",
          "description": "The ordered rules of the profile. Every rule the profile keeps must be sent; the list replaces the current one.",
          "items": {
            "type": "object",
            "properties": {
              "rule": {
                "type": "string",
                "enum": [
                  "Include",
                  "Exclude"
                ],
                "description": "Include makes the matching features available to the tenants of the profile; Exclude hides them."
              },
              "expression": {
                "type": "string",
                "description": "The feature ID the rule matches; a * wildcard matches many, such as 'OrchardCore.Search*'."
              }
            },
            "required": [
              "rule",
              "expression"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [
        "name",
        "rules"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a tenant feature profile: a name and an ordered list of rules, each with a rule name (Include or Exclude) and an expression. It limits which features the tenants using the profile can enable.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageTenantFeatureProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!services.GetRequiredService<ShellSettings>().IsDefaultShell())
        {
            return TenantFeatureProfileToolSupport.DefaultTenantOnlyMessage;
        }

        arguments.TryGetFirstString("id", out var id);
        arguments.TryGetFirstString("name", out var name);

        id = id?.Trim();
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: give the name of the profile.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "rules", out var rules) || rules is null)
        {
            return "Nothing was saved: rules must be a list of { rule, expression } objects.";
        }

        var featureRules = new JsonArray();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i] as JsonObject;
            var ruleName = ReadString(rule, "rule");
            var expression = ReadString(rule, "expression");
            var knownRule = _ruleNames.FirstOrDefault(candidate => string.Equals(candidate, ruleName, StringComparison.OrdinalIgnoreCase));

            if (knownRule is null || string.IsNullOrEmpty(expression))
            {
                return $"Nothing was saved: rule {i + 1} needs a rule of Include or Exclude and an expression.";
            }

            featureRules.Add(new JsonObject
            {
                ["Rule"] = knownRule,
                ["Expression"] = expression,
            });
        }

        var profiles = await services.GetRequiredService<IFeatureProfilesService>().GetFeatureProfilesAsync();
        var created = string.IsNullOrEmpty(id);

        if (!created && !profiles.ContainsKey(id))
        {
            return $"Nothing was saved: there is no feature profile with the ID '{id}'. Leave the id out to create a new profile, or call listTenantFeatureProfiles to find it.";
        }

        // As the Feature Profiles page does: a name another profile has is refused.
        if (profiles.Any(entry => (entry.Value.Name ?? entry.Key) == name && (created || entry.Key != id)))
        {
            return $"Nothing was saved: a feature profile named '{name}' already exists.";
        }

        if (created)
        {
            id = IdGenerator.GenerateId();
        }

        var errors = await RecipeStepRunner.RunAsync(services, TenantFeatureProfileToolSupport.RecipeStepName, new JsonObject
        {
            ["name"] = TenantFeatureProfileToolSupport.RecipeStepName,
            ["FeatureProfiles"] = new JsonObject
            {
                [id] = new JsonObject
                {
                    ["Id"] = id,
                    [NameMember] = name,
                    ["FeatureRules"] = featureRules,
                },
            },
        });

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors)}";
        }

        var tenants = services.GetRequiredService<IShellHost>().GetAllSettings()
            .Where(settings => TenantFeatureProfileToolSupport.GetProfileIds(settings).Contains(id, StringComparer.Ordinal))
            .Select(settings => settings.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return AgentToolResults.Json(new
        {
            saved = true,
            created,
            id,
            name,
            ruleCount = featureRules.Count,
            tenants,
            note = tenants.Count > 0
                ? "The rules limit which features the tenants listed here can enable."
                : "No tenant uses the profile yet. A tenant gets it on its edit page under Multi-Tenancy, Tenants.",
        });
    }

    private static string ReadString(JsonObject node, string name)
    {
        var value = node?.FirstOrDefault(member => string.Equals(member.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

        return value is JsonValue text && text.TryGetValue<string>(out var result)
            ? result.Trim()
            : null;
    }
}
