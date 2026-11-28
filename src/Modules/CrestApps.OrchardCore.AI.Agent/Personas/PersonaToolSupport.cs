using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

namespace CrestApps.OrchardCore.AI.Agent.Personas;

/// <summary>
/// What the AI persona tools share.
/// </summary>
internal static class PersonaToolSupport
{
    /// <summary>
    /// Gets the profiles that link to each persona, by the persona's technical name, which is what a profile keeps.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles of the linked profiles by persona name.</returns>
    public static async Task<Dictionary<string, List<string>>> GetLinkedProfilesAsync(IAIProfileManager profileManager, CancellationToken cancellationToken)
    {
        var linked = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in await profileManager.GetAllAsync(cancellationToken))
        {
            if (!profile.TryGet<AIPersonaMetadata>(out var metadata) || string.IsNullOrWhiteSpace(metadata.PersonaId))
            {
                continue;
            }

            if (!linked.TryGetValue(metadata.PersonaId.Trim(), out var titles))
            {
                linked[metadata.PersonaId.Trim()] = titles = [];
            }

            titles.Add(string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText);
        }

        return linked;
    }

    /// <summary>
    /// Gets the title of a persona for a message.
    /// </summary>
    /// <param name="persona">The persona.</param>
    /// <returns>The title, or the technical name when it has none.</returns>
    public static string GetTitle(AIPersona persona)
        => string.IsNullOrWhiteSpace(persona.DisplayText) ? persona.Name : persona.DisplayText;
}
