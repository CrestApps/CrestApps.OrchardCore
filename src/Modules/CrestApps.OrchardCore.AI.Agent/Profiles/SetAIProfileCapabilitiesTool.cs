using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Sets which tools, tool instances and agents an AI profile can use, and for an agent whether it is always available
/// and whether it runs its tools, as the Capabilities tab of the profile editor does. It refuses the running assistant,
/// any tool the caller may not use, and tools that are not safe on a profile visitors can reach.
/// </summary>
public sealed class SetAIProfileCapabilitiesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setAIProfileCapabilities";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the profile to change, from listAIProfiles."
        },
        "toolNames": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The names of the registered tools the profile can use, from listAITools. Replaces the current list; an empty list removes them all; leave it out to keep them."
        },
        "toolInstanceNames": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The names of the tool instances the profile can use, from listAITools. Replaces the current list; leave it out to keep them."
        },
        "agentNames": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The technical names of the agent profiles this profile may call, from listAITools. Replaces the current list; leave it out to keep them."
        },
        "alwaysAvailable": {
          "type": "boolean",
          "description": "Agents only: whether the agent is offered to every profile on the site, public ones included, instead of only to the profiles that name it."
        },
        "allowToolInvocation": {
          "type": "boolean",
          "description": "Agents only: whether the agent runs its own tools when another profile calls it."
        }
      },
      "required": ["profileId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets which tools, tool instances and agents a profile can use, and for an agent whether it runs its tools and whether it is always available. Refused for the running assistant, for tools you may not use yourself, and for tools that are unsafe on a profile visitors can reach. MCP and A2A connections are set on the profile's Capabilities tab.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "toolNames", out var toolNames) ||
            !AgentToolArguments.TryReadStrings(arguments, "toolInstanceNames", out var instanceNames) ||
            !AgentToolArguments.TryReadStrings(arguments, "agentNames", out var agentNames))
        {
            return "Nothing was changed: toolNames, toolInstanceNames and agentNames must each be a list of names.";
        }

        bool? alwaysAvailable = arguments.TryGetFirst<bool>("alwaysAvailable", out var available) ? available : null;
        bool? allowToolInvocation = arguments.TryGetFirst<bool>("allowToolInvocation", out var allow) ? allow : null;

        if (toolNames is null && instanceNames is null && agentNames is null && alwaysAvailable is null && allowToolInvocation is null)
        {
            return "Nothing was changed: give at least one of toolNames, toolInstanceNames, agentNames, alwaysAvailable or allowToolInvocation.";
        }

        arguments.TryGetFirstString("profileId", out var profileId);
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await profileManager.FindByIdAsync(profileId.Trim(), cancellationToken);

        if (profile is null)
        {
            return $"Nothing was changed: no AI profile has the ID '{profileId}'. Use listAIProfiles to find the ID.";
        }

        var title = AIConfigurationGuard.GetTitle(profile);
        var editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId);

        if ((await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken)).IsProtected(profile))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the profile '{title}'", editPath);
        }

        if ((alwaysAvailable is not null || allowToolInvocation is not null) && profile.Type != AIProfileType.Agent)
        {
            return $"Nothing was changed: alwaysAvailable and allowToolInvocation apply to agents only, and '{title}' is a {profile.Type} profile.";
        }

        var error = await ValidateNamesAsync(services, profile, toolNames, instanceNames, agentNames, cancellationToken);

        if (error is not null)
        {
            return $"Nothing was changed: {error}";
        }

        // Check the result on a copy first; the stored profile is only touched once every rule passed.
        var copy = profile.Clone();
        Apply(copy, toolNames, instanceNames, agentNames, alwaysAvailable, allowToolInvocation);

        var refusal = await AIConfigurationGuard.CheckCapabilityChangeAsync(services, profile, copy, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        Apply(profile, toolNames, instanceNames, agentNames, alwaysAvailable, allowToolInvocation);
        AIProfileToolSupport.SetVersionNote(services, profile, "Capabilities changed");

        await profileManager.UpdateAsync(profile, cancellationToken: cancellationToken);

        var capabilities = AIProfileCapabilities.Read(profile);

        return AgentToolResults.Json(new
        {
            updated = true,
            id = profile.ItemId,
            name = profile.Name,
            title,
            toolNames = capabilities.ToolNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            toolInstanceNames = capabilities.ToolInstanceNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            agentNames = capabilities.AgentNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            mcpConnectionCount = capabilities.McpConnectionIds.Count,
            a2aConnectionCount = capabilities.A2AConnectionIds.Count,
            alwaysAvailable = profile.Type == AIProfileType.Agent ? capabilities.IsAlwaysAvailableAgent : (bool?)null,
            allowToolInvocation = profile.Type == AIProfileType.Agent ? capabilities.AllowsToolInvocation : (bool?)null,
            editPath,
            versionsPath = AIProfileToolSupport.GetProfileVersionsPath(services, profile.ItemId),
        });
    }

    private static async Task<string> ValidateNamesAsync(
        IServiceProvider services,
        AIProfile profile,
        List<string> toolNames,
        List<string> instanceNames,
        List<string> agentNames,
        CancellationToken cancellationToken)
    {
        if (toolNames is { Count: > 0 })
        {
            var selectable = services.GetService<IOptions<AIToolDefinitionOptions>>()?.Value.GetSelectableTools()
                ?? new Dictionary<string, AIToolDefinitionEntry>();
            var unknown = toolNames.Where(name => !selectable.ContainsKey(name)).ToList();

            if (unknown.Count > 0)
            {
                return $"no tool that can be picked is named {string.Join(", ", unknown.Select(name => $"'{name}'"))}. Use listAITools to find the names.";
            }
        }

        if (instanceNames is { Count: > 0 })
        {
            var catalog = services.GetService<INamedCatalog<AIToolInstance>>();

            if (catalog is null)
            {
                return "tool instances are not enabled on this site.";
            }

            foreach (var instanceName in instanceNames)
            {
                if (await catalog.FindByNameAsync(instanceName, cancellationToken) is null)
                {
                    return $"no tool instance is named '{instanceName}'. Use listAITools to find the names.";
                }
            }
        }

        if (agentNames is { Count: > 0 })
        {
            var profileManager = services.GetRequiredService<IAIProfileManager>();

            foreach (var agentName in agentNames)
            {
                var agent = await profileManager.FindByNameAsync(agentName, cancellationToken);

                if (agent is null || agent.Type != AIProfileType.Agent)
                {
                    return $"no agent profile is named '{agentName}'. Use listAITools with kind agent to find the names.";
                }

                if (string.Equals(agent.ItemId, profile.ItemId, StringComparison.Ordinal))
                {
                    return "a profile cannot call itself as an agent.";
                }
            }
        }

        return null;
    }

    private static void Apply(
        AIProfile profile,
        List<string> toolNames,
        List<string> instanceNames,
        List<string> agentNames,
        bool? alwaysAvailable,
        bool? allowToolInvocation)
    {
        if (toolNames is not null)
        {
            profile.Put(new FunctionInvocationMetadata { Names = [.. toolNames] });
        }

        if (instanceNames is not null)
        {
            profile.Put(new AIToolInstanceMetadata { ToolInstanceNames = [.. instanceNames] });
        }

        if (agentNames is not null)
        {
            profile.Put(new AgentInvocationMetadata { Names = [.. agentNames] });
        }

        if (alwaysAvailable is not null || allowToolInvocation is not null)
        {
            var agent = profile.GetOrCreate<AgentMetadata>();

            if (alwaysAvailable is not null)
            {
                agent.Availability = alwaysAvailable.Value ? AgentAvailability.AlwaysAvailable : AgentAvailability.OnDemand;
            }

            if (allowToolInvocation is not null)
            {
                agent.AllowToolInvocation = allowToolInvocation.Value;
            }

            profile.Put(agent);
        }
    }
}
