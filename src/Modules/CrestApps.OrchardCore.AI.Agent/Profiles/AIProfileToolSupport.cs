using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// What the AI profile, template, persona and tool instance tools share: finding a profile, the admin links, the
/// refusal for the running assistant and the version note.
/// </summary>
internal static class AIProfileToolSupport
{
    /// <summary>
    /// Finds a profile by its ID, or by its technical name when no ID is given.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="profileId">The ID, or <see langword="null"/>.</param>
    /// <param name="profileName">The technical name, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile, or <see langword="null"/> when none matches.</returns>
    public static async Task<AIProfile> FindProfileAsync(IAIProfileManager profileManager, string profileId, string profileName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return await profileManager.FindByIdAsync(profileId.Trim(), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            return await profileManager.FindByNameAsync(profileName.Trim(), cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Gets the address of a profile's editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="profileId">The ID of the profile.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetProfileEditPath(IServiceProvider services, string profileId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "Profiles", "Edit", new RouteValueDictionary { ["id"] = profileId });

    /// <summary>
    /// Gets the address of a profile's versions page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="profileId">The ID of the profile.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetProfileVersionsPath(IServiceProvider services, string profileId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIProfileVersions", "Index", new RouteValueDictionary { ["profileId"] = profileId });

    /// <summary>
    /// Gets the address of a template's editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="templateId">The ID of the template.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetTemplateEditPath(IServiceProvider services, string templateId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AITemplates", "Edit", new RouteValueDictionary { ["id"] = templateId });

    /// <summary>
    /// Gets the address of a persona's editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="personaId">The ID of the persona.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetPersonaEditPath(IServiceProvider services, string personaId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIPersonas", "Edit", new RouteValueDictionary { ["id"] = personaId });

    /// <summary>
    /// Gets the address of a tool instance's editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="instanceId">The ID of the tool instance.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetToolInstanceEditPath(IServiceProvider services, string instanceId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIToolInstances", "Edit", new RouteValueDictionary { ["id"] = instanceId });

    /// <summary>
    /// The refusal for a change to something the running assistant is built from.
    /// </summary>
    /// <param name="what">What was to be changed, for example "the profile 'Support'".</param>
    /// <param name="path">The admin page where a person can make the change, or <see langword="null"/>.</param>
    /// <returns>The refusal.</returns>
    public static string RunningAssistantRefusal(string what, string path)
        => $"Refused: {what} is part of the assistant running this chat (its profile, its agents, the admin chat widget's assistant, or the persona, prompt templates or tool instances they use). An assistant cannot change itself from a chat. A person can make this change on the admin page{(string.IsNullOrEmpty(path) ? "." : $": {path}")}";

    /// <summary>
    /// Writes the note the profile's next version shows, so the Versions page tells who made the change.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="profile">The profile about to be saved.</param>
    /// <param name="action">What was done, for example "Changed" or "Restored from version 3".</param>
    public static void SetVersionNote(IServiceProvider services, AIProfile profile, string action)
    {
        var userName = AgentToolAuthorization.GetCaller(services)?.Identity?.Name;
        var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();

        metadata.PendingNote = string.IsNullOrEmpty(userName)
            ? $"{action} by the AI assistant"
            : $"{action} by the AI assistant for {userName}";

        profile.Put(metadata);
    }

    /// <summary>
    /// Gets the saved version number of a profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The version number, or <see langword="null"/> when the profile has no saved version.</returns>
    public static int? GetVersion(AIProfile profile)
        => profile.TryGet<AIProfileVersionMetadata>(out var metadata) && metadata.Version > 0 ? metadata.Version : null;
}
