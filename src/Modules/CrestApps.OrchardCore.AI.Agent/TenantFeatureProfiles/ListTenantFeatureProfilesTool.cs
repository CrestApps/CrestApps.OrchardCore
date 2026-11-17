using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;

/// <summary>
/// Lists the tenant feature profiles from the feature profiles service, as the Feature Profiles page does, with the
/// tenants that use each one. It works from the Default tenant only, as that page does.
/// </summary>
public sealed class ListTenantFeatureProfilesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTenantFeatureProfiles";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name or ID of a profile must contain. Leave it out to list every profile."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of profiles on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the tenant feature profiles with their include and exclude rules and the tenants that use each one.";

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

        arguments.TryGetFirstString("search", out var search);
        search = search?.Trim();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var profiles = await services.GetRequiredService<IFeatureProfilesService>().GetFeatureProfilesAsync();
        var tenants = services.GetRequiredService<IShellHost>().GetAllSettings().ToList();

        var filtered = profiles
            .Select(entry => (Id: entry.Key, Profile: entry.Value, Name: entry.Value.Name ?? entry.Key))
            .Where(entry => string.IsNullOrEmpty(search) ||
                entry.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return AgentToolResults.Json(new
        {
            items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new
                {
                    id = entry.Id,
                    name = entry.Name,
                    rules = (entry.Profile.FeatureRules ?? [])
                        .Select(rule => new
                        {
                            rule = rule.Rule,
                            expression = rule.Expression,
                        })
                        .ToList(),
                    tenants = tenants
                        .Where(settings => TenantFeatureProfileToolSupport.GetProfileIds(settings).Contains(entry.Id, StringComparer.Ordinal))
                        .Select(settings => settings.Name)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                })
                .ToList(),
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize
                ? true
                : (bool?)null,
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.Tenants", "FeatureProfiles", "Index"),
        });
    }
}
