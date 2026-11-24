using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Checks what would stop one AI profile from working: its deployments, whether the chat deployment can call tools,
/// and whether every tool, tool instance, agent, persona and data source it names still exists. It reads only.
/// </summary>
internal sealed class DiagnoseAIProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "diagnoseAIProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the profile to check, from listAIProfiles. Give this or profileName."
        },
        "profileName": {
          "type": "string",
          "description": "The technical name of the profile to check, when the ID is not known."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Checks one profile for what would stop it working: whether its chat and utility deployments exist and the chat one supports tool calling, whether every tool, tool instance and agent it names still exists, and whether its persona and data source exist. Returns each check with a fix.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("profileId", out var profileId);
        arguments.TryGetFirstString("profileName", out var profileName);

        if (string.IsNullOrWhiteSpace(profileId) && string.IsNullOrWhiteSpace(profileName))
        {
            return "Nothing was checked: give the profileId or the profileName of the profile to check.";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = await AIProfileToolSupport.FindProfileAsync(profileManager, profileId, profileName, cancellationToken);

        if (profile is null)
        {
            return $"Nothing was checked: no AI profile has the {(string.IsNullOrWhiteSpace(profileId) ? $"name '{profileName?.Trim()}'" : $"ID '{profileId.Trim()}'")}. Use listAIProfiles to find it.";
        }

        var editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId);
        var capabilities = AIProfileCapabilities.Read(profile);
        var checks = new List<object>();
        var problems = 0;

        void Add(string check, bool ok, string detail, string fix)
        {
            checks.Add(new { check, ok, detail, fix = ok ? null : fix });

            if (!ok)
            {
                problems++;
            }
        }

        await CheckDeploymentsAsync(services, profile, capabilities, editPath, Add, cancellationToken);
        CheckTools(services, capabilities, editPath, Add);
        await CheckToolInstancesAsync(services, capabilities, editPath, Add, cancellationToken);
        await CheckAgentsAsync(profileManager, capabilities, editPath, Add, cancellationToken);
        await CheckPersonaAsync(services, profile, editPath, Add, cancellationToken);
        await CheckDataSourceAsync(services, profile, editPath, Add, cancellationToken);

        return AgentToolResults.Json(new
        {
            id = profile.ItemId,
            name = profile.Name,
            title = AIConfigurationGuard.GetTitle(profile),
            type = profile.Type.ToString(),
            healthy = problems == 0,
            problems,
            checks,
            editPath,
        });
    }

    private static async Task CheckDeploymentsAsync(
        IServiceProvider services,
        AIProfile profile,
        AIProfileCapabilities capabilities,
        string editPath,
        Action<string, bool, string, string> add,
        CancellationToken cancellationToken)
    {
        var deploymentManager = services.GetService<IAIDeploymentManager>();

        if (deploymentManager is null)
        {
            add("chatDeployment", false, "No deployment manager is registered, so the profile cannot reach a model.", "Enable an AI provider feature and add a deployment.");

            return;
        }

        foreach (var (slot, label, name) in new[]
        {
            (AIDeploymentSlotNames.Chat, "chatDeployment", profile.ChatDeploymentName),
            (AIDeploymentSlotNames.Utility, "utilityDeployment", profile.UtilityDeploymentName),
        })
        {
            var deploymentName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            var deployment = deploymentName is null
                ? await deploymentManager.ResolveSlotAsync(slot, cancellationToken: cancellationToken)
                : await deploymentManager.FindByNameAsync(deploymentName, cancellationToken);

            if (deployment is null)
            {
                add(label, false,
                    deploymentName is null
                        ? $"The profile names no {slot} deployment and the site has no default one."
                        : $"The {slot} deployment '{deploymentName}' does not exist.",
                    $"Pick an existing deployment on the profile's page ({editPath}) or set the site default under AI settings. listAIDeployments shows the deployments.");

                continue;
            }

            add(label, true, deploymentName is null
                ? $"Uses the site default {slot} deployment '{deployment.Name}'."
                : $"The {slot} deployment '{deployment.Name}' exists.", null);

            if (slot != AIDeploymentSlotNames.Chat || (!capabilities.HasAnyTool && capabilities.AgentNames.Count == 0))
            {
                continue;
            }

            var capabilityService = services.GetService<IAIDeploymentCapabilityService>();
            var supportsTools = capabilityService?.SupportsFeatureOrUnconstrained(deployment, AIDeploymentFeatureNames.ToolCalling)
                ?? AIModelToolSupport.GetFeatures(deployment) is not { Length: > 0 } features ||
                    features.Contains(AIDeploymentFeatureNames.ToolCalling, StringComparer.OrdinalIgnoreCase);

            add("toolCalling", supportsTools,
                supportsTools
                    ? $"The chat deployment '{deployment.Name}' can call tools (or declares no limits)."
                    : $"The profile has tools or agents, but the chat deployment '{deployment.Name}' does not declare tool calling, so the model will not use them.",
                $"Pick a chat deployment that supports tool calling on the profile's page ({editPath}), or add toolCalling to the deployment's capabilities if its model supports it.");
        }
    }

    private static void CheckTools(IServiceProvider services, AIProfileCapabilities capabilities, string editPath, Action<string, bool, string, string> add)
    {
        if (capabilities.ToolNames.Count == 0)
        {
            return;
        }

        var registered = services.GetService<IOptions<AIToolDefinitionOptions>>()?.Value.Tools
            ?? new Dictionary<string, AIToolDefinitionEntry>();
        var missing = capabilities.ToolNames
            .Where(name => !registered.ContainsKey(name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        add("tools", missing.Count == 0,
            missing.Count == 0
                ? $"All {capabilities.ToolNames.Count} tools the profile names are registered."
                : $"These tools are not registered on the site (renamed, removed, or their feature is off): {string.Join(", ", missing)}.",
            $"Remove or replace them with setAIProfileCapabilities, or enable the feature that provides them. listAITools shows the tools. Profile page: {editPath}");
    }

    private static async Task CheckToolInstancesAsync(IServiceProvider services, AIProfileCapabilities capabilities, string editPath, Action<string, bool, string, string> add, CancellationToken cancellationToken)
    {
        if (capabilities.ToolInstanceNames.Count == 0)
        {
            return;
        }

        var catalog = services.GetService<INamedCatalog<AIToolInstance>>();
        var missing = new List<string>();

        foreach (var instanceName in capabilities.ToolInstanceNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (catalog is null || await catalog.FindByNameAsync(instanceName, cancellationToken) is null)
            {
                missing.Add(instanceName);
            }
        }

        add("toolInstances", missing.Count == 0,
            missing.Count == 0
                ? $"All {capabilities.ToolInstanceNames.Count} tool instances the profile names exist."
                : $"These tool instances do not exist (deleted, or tool instances are off): {string.Join(", ", missing)}.",
            $"Remove them with setAIProfileCapabilities or create them again. Profile page: {editPath}");
    }

    private static async Task CheckAgentsAsync(IAIProfileManager profileManager, AIProfileCapabilities capabilities, string editPath, Action<string, bool, string, string> add, CancellationToken cancellationToken)
    {
        if (capabilities.AgentNames.Count == 0)
        {
            return;
        }

        var problems = new List<string>();

        foreach (var agentName in capabilities.AgentNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            var agent = await profileManager.FindByNameAsync(agentName, cancellationToken);

            if (agent is null)
            {
                problems.Add($"'{agentName}' does not exist");
            }
            else if (agent.Type != AIProfileType.Agent)
            {
                problems.Add($"'{agentName}' is a {agent.Type} profile, not an agent");
            }
        }

        add("agents", problems.Count == 0,
            problems.Count == 0
                ? $"All {capabilities.AgentNames.Count} agents the profile calls exist."
                : $"Some agents cannot be called: {string.Join("; ", problems)}.",
            $"Remove or replace them with setAIProfileCapabilities. listAIProfiles with type Agent shows the agents. Profile page: {editPath}");
    }

    private static async Task CheckPersonaAsync(IServiceProvider services, AIProfile profile, string editPath, Action<string, bool, string, string> add, CancellationToken cancellationToken)
    {
        if (!profile.TryGet<AIPersonaMetadata>(out var metadata) || string.IsNullOrWhiteSpace(metadata.PersonaId))
        {
            return;
        }

        var personaName = metadata.PersonaId.Trim();
        var personaManager = services.GetService<IAIPersonaManager>();
        var exists = personaManager is not null && await personaManager.FindByNameAsync(personaName, cancellationToken) is not null;

        add("persona", exists,
            exists
                ? $"The persona '{personaName}' exists."
                : personaManager is null
                    ? $"The profile speaks as the persona '{personaName}', but personas are not enabled."
                    : $"The persona '{personaName}' does not exist.",
            $"Pick another persona with saveAIProfile (personaName), or clear it with an empty personaName. Profile page: {editPath}");
    }

    private static async Task CheckDataSourceAsync(IServiceProvider services, AIProfile profile, string editPath, Action<string, bool, string, string> add, CancellationToken cancellationToken)
    {
        if (!profile.TryGet<DataSourceMetadata>(out var metadata) || string.IsNullOrWhiteSpace(metadata.DataSourceId))
        {
            return;
        }

        var dataSourceId = metadata.DataSourceId.Trim();
        var manager = services.GetService<ISourceCatalogManager<AIDataSource>>();
        var dataSource = manager is null ? null : await manager.FindByIdAsync(dataSourceId, cancellationToken);

        add("dataSource", dataSource is not null,
            dataSource is not null
                ? $"The data source '{dataSource.DisplayText ?? dataSourceId}' exists."
                : manager is null
                    ? $"The profile answers from the data source '{dataSourceId}', but data sources are not enabled."
                    : $"The data source '{dataSourceId}' does not exist.",
            $"Pick another data source on the profile's page: {editPath}. listAIDataSources shows the data sources.");
    }
}
