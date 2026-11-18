using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// The rules every tool that changes an AI profile, template, persona or tool instance applies, until the site-wide
/// configuration guard exists:
/// <list type="bullet">
/// <item>From inside an AI turn, nothing the running assistant is built from can be changed or deleted (rule A).</item>
/// <item>A profile that callers who are not signed in can reach may only gain tools marked safe for them (rule B).</item>
/// <item>Each added tool needs the caller's own permission to use it, as the profile editor asks (rule C).</item>
/// </list>
/// </summary>
internal static class AIConfigurationGuard
{
    /// <summary>
    /// The key of the admin chat widget's settings in the site settings. The settings type lives in the AI Chat
    /// module, which this module does not reference, so the entry is read by its name.
    /// </summary>
    public const string AdminWidgetSettingsKey = "AIChatAdminWidgetSettings";

    /// <summary>
    /// How deep the guard follows agents that call agents.
    /// </summary>
    private const int MaxAgentDepth = 8;

    /// <summary>
    /// Gets everything the assistant running the current AI turn is built from. Outside an AI turn nothing is
    /// protected, so the admin pages and background work are not affected.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The protected configuration.</returns>
    public static async Task<AIProtectedConfiguration> GetProtectedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var invocation = AIInvocationScope.Current;

        if (invocation is null)
        {
            return AIProtectedConfiguration.None;
        }

        try
        {
            return await BuildProtectedAsync(services, invocation, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // When the running assistant cannot be worked out, everything counts as protected.
            return AIProtectedConfiguration.Everything;
        }
    }

    /// <summary>
    /// Gets the deployments and provider connections the assistant running the current AI turn talks to. Outside an
    /// AI turn nothing is protected.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The protected deployments and connections.</returns>
    public static async Task<AIProtectedModels> GetProtectedModelsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var invocation = AIInvocationScope.Current;

        if (invocation is null)
        {
            return AIProtectedModels.None;
        }

        var configuration = await GetProtectedAsync(services, cancellationToken);

        if (ReferenceEquals(configuration, AIProtectedConfiguration.Everything))
        {
            return AIProtectedModels.Everything;
        }

        try
        {
            return await BuildProtectedModelsAsync(services, invocation, configuration, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // When the running assistant's models cannot be worked out, every one counts as protected.
            return AIProtectedModels.Everything;
        }
    }

    /// <summary>
    /// Gets the ways callers who are not signed in can reach a profile. Bindings of other features, such as a phone
    /// number or a messaging channel, are not checked here.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="profile">The profile as it is, or as it will be saved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A sentence per way in; empty when only signed-in callers with a permission can reach it.</returns>
    public static async Task<List<string>> GetUntrustedReachAsync(IServiceProvider services, AIProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(profile);

        var reasons = new List<string>();

        // The real role and dynamic-permission handlers decide, as they do for a visitor's chat.
        var authorizationService = services.GetService<IAuthorizationService>();
        var visitor = new ClaimsPrincipal(new ClaimsIdentity());

        if (authorizationService is not null &&
            await authorizationService.AuthorizeAsync(visitor, AIPermissions.QueryAnyAIProfile, profile))
        {
            reasons.Add("the Anonymous role may chat with it");
        }

        var apiKeyStore = services.GetService<IAIApiKeyStore>();

        if (apiKeyStore is not null && !string.IsNullOrEmpty(profile.ItemId))
        {
            var now = services.GetService<IClock>()?.UtcNow ?? (services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var keys = await apiKeyStore.GetAllAsync(cancellationToken);

            if (keys.Any(key => key.Kind == AIApiKeyKind.Browser &&
                key.Enabled &&
                (key.ExpiresUtc is null || key.ExpiresUtc > now) &&
                key.AllowedProfileIds?.Contains(profile.ItemId, StringComparer.Ordinal) == true))
            {
                reasons.Add("a browser API key, which is public, may chat with it");
            }
        }

        var widgetFinder = services.GetService<IPublishedChatWidgetFinder>();

        if (widgetFinder is not null && !string.IsNullOrEmpty(profile.ItemId) && await widgetFinder.CountAsync(profile.ItemId) > 0)
        {
            reasons.Add("a published content item, such as a chat widget, shows it to visitors");
        }

        if (AIProfileCapabilities.Read(profile).IsAlwaysAvailableAgent)
        {
            reasons.Add("it is an always-available agent, which every profile on the site can call, public ones included");
        }

        return reasons;
    }

    /// <summary>
    /// Checks the tools a change gives a profile: on a profile untrusted callers can reach only tools marked safe for
    /// them may be added, and the caller must be allowed to use every tool and tool instance added (rules B and C).
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="before">The profile before the change, or <see langword="null"/> for a new profile.</param>
    /// <param name="after">The profile as it will be saved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refusal to return, or <see langword="null"/> when the change may be saved.</returns>
    public static async Task<string> CheckCapabilityChangeAsync(IServiceProvider services, AIProfile before, AIProfile after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(after);

        var current = AIProfileCapabilities.Read(before);
        var next = AIProfileCapabilities.Read(after);

        // Becoming an always-available agent opens the profile to every caller, and an always-available agent that
        // starts running its tools runs them for every caller, so all of it is checked, not only what the change adds.
        var opensUp = next.IsAlwaysAvailableAgent &&
            (!current.IsAlwaysAvailableAgent || (next.AllowsToolInvocation && !current.AllowsToolInvocation));

        var added = opensUp
            ? next.Except(new AIProfileCapabilities())
            : next.Except(current);

        var denied = await GetToolsTheCallerMayNotUseAsync(services, next.Except(current), cancellationToken);

        if (denied.Count > 0)
        {
            return $"Nothing was changed: you are not allowed to use {Join(denied)}, so you cannot give it to a profile either. A person with that permission can add it on the profile's Capabilities tab.";
        }

        if (!added.HasAnyTool && added.AgentNames.Count == 0)
        {
            return null;
        }

        var reach = await GetUntrustedReachAsync(services, after, cancellationToken);

        if (reach.Count == 0)
        {
            return null;
        }

        var unsafeItems = await GetUnsafeForUntrustedCallersAsync(services, added, cancellationToken);

        if (unsafeItems.Count == 0)
        {
            return null;
        }

        return $"Nothing was changed: callers who are not signed in can reach the profile '{GetTitle(after)}' ({string.Join("; ", reach)}), and {Join(unsafeItems)} {(unsafeItems.Count == 1 ? "is" : "are")} not marked safe for them. Only tools marked safe for anonymous callers may be added to such a profile; tool instances, MCP and A2A connections and agents that run tools may not. Remove the public access first, or change the profile on its admin page if you are sure.";
    }

    /// <summary>
    /// Gets the title of a profile for a message.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The title, or the technical name when it has none.</returns>
    public static string GetTitle(AIProfile profile)
        => string.IsNullOrWhiteSpace(profile?.DisplayText) ? profile?.Name : profile.DisplayText;

    private static async Task<List<string>> GetToolsTheCallerMayNotUseAsync(IServiceProvider services, AIProfileCapabilities added, CancellationToken cancellationToken)
    {
        var denied = new List<string>();

        if (added.ToolNames.Count == 0 && added.ToolInstanceNames.Count == 0)
        {
            return denied;
        }

        var caller = AgentToolAuthorization.GetCaller(services);
        var evaluator = services.GetService<IAIToolAccessEvaluator>();

        if (caller is null || evaluator is null)
        {
            denied.AddRange(added.ToolNames.Select(name => $"the tool '{name}'"));
            denied.AddRange(added.ToolInstanceNames.Select(name => $"the tool instance '{name}'"));

            return denied;
        }

        foreach (var toolName in added.ToolNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!await evaluator.IsAuthorizedAsync(caller, toolName))
            {
                denied.Add($"the tool '{toolName}'");
            }
        }

        if (added.ToolInstanceNames.Count == 0)
        {
            return denied;
        }

        var instances = services.GetService<INamedCatalog<AIToolInstance>>();

        foreach (var instanceName in added.ToolInstanceNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            var instance = instances is null ? null : await instances.FindByNameAsync(instanceName, cancellationToken);

            if (instance is null || !await evaluator.IsAuthorizedAsync(caller, instance.GetFunctionName()))
            {
                denied.Add($"the tool instance '{instanceName}'");
            }
        }

        return denied;
    }

    private static async Task<List<string>> GetUnsafeForUntrustedCallersAsync(IServiceProvider services, AIProfileCapabilities added, CancellationToken cancellationToken)
    {
        var anonymousTools = services.GetService<IOptions<AnonymousAIToolOptions>>()?.Value;
        var unsafeItems = new List<string>();

        unsafeItems.AddRange(added.ToolNames
            .Where(name => anonymousTools?.IsSafe(name) != true)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => $"the tool '{name}'"));

        // A tool instance has no anonymous-safe marking, so none is allowed.
        unsafeItems.AddRange(added.ToolInstanceNames.Order(StringComparer.OrdinalIgnoreCase).Select(name => $"the tool instance '{name}'"));
        unsafeItems.AddRange(added.McpConnectionIds.Order(StringComparer.Ordinal).Select(id => $"the MCP connection '{id}'"));
        unsafeItems.AddRange(added.A2AConnectionIds.Order(StringComparer.Ordinal).Select(id => $"the A2A connection '{id}'"));

        if (added.AgentNames.Count > 0)
        {
            var profileManager = services.GetRequiredService<IAIProfileManager>();

            foreach (var agentName in added.AgentNames.Order(StringComparer.OrdinalIgnoreCase))
            {
                var agent = await profileManager.FindByNameAsync(agentName, cancellationToken);
                var agentCapabilities = AIProfileCapabilities.Read(agent);

                // An agent that is not found, or that runs tools of its own, could do on the caller's behalf what the
                // caller may not.
                if (agent is null || (agentCapabilities.AllowsToolInvocation && agentCapabilities.HasAnyTool))
                {
                    unsafeItems.Add($"the agent '{agentName}', which runs tools");
                }
            }
        }

        return unsafeItems;
    }

    private static async Task<AIProtectedConfiguration> BuildProtectedAsync(IServiceProvider services, AIInvocationContext invocation, CancellationToken cancellationToken)
    {
        var configuration = new AIProtectedConfiguration();
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var pending = new Queue<(AIProfile Profile, int Depth)>();

        void Enqueue(AIProfile profile, int depth)
        {
            if (profile is not null && depth <= MaxAgentDepth && configuration.AddProfile(profile))
            {
                pending.Enqueue((profile, depth));
            }
        }

        var resource = invocation.ToolExecutionContext?.Resource;

        if (resource is AIProfile runningProfile)
        {
            // The stored copy is what an edit would change, and the running copy is what the turn uses.
            Enqueue(runningProfile, 0);
            Enqueue(string.IsNullOrEmpty(runningProfile.ItemId) ? null : await profileManager.FindByIdAsync(runningProfile.ItemId, cancellationToken), 0);
        }
        else if (resource is ChatInteraction interaction)
        {
            configuration.AddFrom(interaction);

            foreach (var agentName in interaction.AgentNames ?? [])
            {
                Enqueue(await profileManager.FindByNameAsync(agentName, cancellationToken), 1);
            }
        }
        else if (resource is ExtensibleEntity entity)
        {
            configuration.AddFrom(entity);
        }

        var sessionProfileId = invocation.ChatSession?.ProfileId;

        if (!string.IsNullOrEmpty(sessionProfileId))
        {
            configuration.ProfileIds.Add(sessionProfileId);
            Enqueue(await profileManager.FindByIdAsync(sessionProfileId, cancellationToken), 0);
        }

        var widgetProfileId = await GetAdminWidgetProfileIdAsync(services);

        if (!string.IsNullOrEmpty(widgetProfileId))
        {
            configuration.ProfileIds.Add(widgetProfileId);
            Enqueue(await profileManager.FindByIdAsync(widgetProfileId, cancellationToken), 0);
        }

        // An always-available agent is offered to the running assistant too, whatever it names.
        foreach (var agent in await profileManager.GetAsync(AIProfileType.Agent, cancellationToken))
        {
            if (AIProfileCapabilities.Read(agent).IsAlwaysAvailableAgent)
            {
                Enqueue(agent, 1);
            }
        }

        while (pending.TryDequeue(out var item))
        {
            configuration.AddFrom(item.Profile);

            foreach (var agentName in AIProfileCapabilities.Read(item.Profile).AgentNames)
            {
                Enqueue(await profileManager.FindByNameAsync(agentName, cancellationToken), item.Depth + 1);
            }
        }

        return configuration;
    }

    private static async Task<AIProtectedModels> BuildProtectedModelsAsync(IServiceProvider services, AIInvocationContext invocation, AIProtectedConfiguration configuration, CancellationToken cancellationToken)
    {
        var deploymentManager = services.GetService<IAIDeploymentManager>();

        if (deploymentManager is null)
        {
            return AIProtectedModels.Everything;
        }

        var models = new AIProtectedModels();
        var profileManager = services.GetRequiredService<IAIProfileManager>();

        // A profile that names no deployment uses the site's default, so the defaults always count.
        var requested = new List<(string Slot, string Name)>
        {
            (AIDeploymentSlotNames.Chat, null),
            (AIDeploymentSlotNames.Utility, null),
        };

        foreach (var profileId in configuration.ProfileIds.Order(StringComparer.Ordinal))
        {
            var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

            if (profile is not null)
            {
                requested.Add((AIDeploymentSlotNames.Chat, profile.ChatDeploymentName));
                requested.Add((AIDeploymentSlotNames.Utility, profile.UtilityDeploymentName));
            }
        }

        if (invocation.ToolExecutionContext?.Resource is ChatInteraction interaction)
        {
            requested.Add((AIDeploymentSlotNames.Chat, interaction.ChatDeploymentName));
            requested.Add((AIDeploymentSlotNames.Utility, interaction.UtilityDeploymentName));
        }

        foreach (var (slot, name) in requested)
        {
            var deploymentName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

            if (deploymentName is not null)
            {
                models.Add(await deploymentManager.FindByNameAsync(deploymentName, cancellationToken));
            }

            models.Add(await deploymentManager.ResolveSlotAsync(slot, deploymentName: deploymentName, cancellationToken: cancellationToken));
        }

        var personaManager = services.GetService<IAIPersonaManager>();

        if (personaManager is not null)
        {
            foreach (var personaName in configuration.PersonaNames.Order(StringComparer.OrdinalIgnoreCase))
            {
                var persona = await personaManager.FindByNameAsync(personaName, cancellationToken);

                if (!string.IsNullOrWhiteSpace(persona?.VoiceDeploymentName))
                {
                    models.Add(await deploymentManager.FindByNameAsync(persona.VoiceDeploymentName.Trim(), cancellationToken));
                }
            }
        }

        return models;
    }

    private static async Task<string> GetAdminWidgetProfileIdAsync(IServiceProvider services)
    {
        var siteService = services.GetService<ISiteService>();

        if (siteService is null)
        {
            return null;
        }

        var site = await siteService.GetSiteSettingsAsync();
        var node = site?.Properties?[AdminWidgetSettingsKey]?["ProfileId"];

        return node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
    }

    private static string Join(List<string> items)
        => items.Count switch
        {
            1 => items[0],
            2 => $"{items[0]} and {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
        };
}
