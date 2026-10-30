using CrestApps.Core;
using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Keeps the assistant's own name in a conversation whose personal details are redacted: the name of the AI persona
/// the conversation started with, and of the persona its profile uses now, if a call was handed to another profile.
/// </summary>
/// <remarks>
/// Live, a call's transcript came back with the assistant's name redacted wherever it introduced itself.
/// </remarks>
public sealed class AIPersonaNamePrivacyHandler : AIConversationPrivacyHandlerBase
{
    private readonly IAIPersonaManager _personaManager;
    private readonly IAIProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaNamePrivacyHandler"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager.</param>
    /// <param name="profileManager">The profile manager the conversation's profile is read from.</param>
    public AIPersonaNamePrivacyHandler(IAIPersonaManager personaManager, IAIProfileManager profileManager)
    {
        _personaManager = personaManager;
        _profileManager = profileManager;
    }

    /// <inheritdoc/>
    public override async Task CollectingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        var personaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(context.Session.PersonaId))
        {
            personaIds.Add(context.Session.PersonaId);
        }

        if (!string.IsNullOrWhiteSpace(context.Session.ProfileId) &&
            await _profileManager.FindByIdAsync(context.Session.ProfileId, cancellationToken) is { } profile &&
            profile.TryGet<AIPersonaMetadata>(out var metadata) &&
            !string.IsNullOrWhiteSpace(metadata.PersonaId))
        {
            personaIds.Add(metadata.PersonaId);
        }

        foreach (var personaId in personaIds)
        {
            // A profile links a persona by its technical name.
            var persona = await _personaManager.FindByNameAsync(personaId, cancellationToken)
                ?? await _personaManager.FindByIdAsync(personaId, cancellationToken);

            AINamesToKeep.Add(context, persona?.AssistantName);
        }
    }
}
