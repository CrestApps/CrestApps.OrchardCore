using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.ViewModels;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Fills and applies the persona picker shared by the AI profile, profile template, chat interaction and
/// workflow task editors.
/// </summary>
public static class AIPersonaPickerExtensions
{
    /// <summary>
    /// Fills <paramref name="model"/> with the persona <paramref name="entity"/> links to and every persona it can
    /// link to.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists saved personas and persona files.</param>
    /// <param name="model">The picker model to fill.</param>
    /// <param name="entity">The entity whose link is shown, or <see langword="null"/> for a new one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task PopulatePersonaPickerAsync(
        this IAIPersonaManager personaManager,
        AIPersonaPickerViewModel model,
        ExtensibleEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(personaManager);
        ArgumentNullException.ThrowIfNull(model);

        model.PersonaId = entity is not null && entity.TryGet<AIPersonaMetadata>(out var metadata)
            ? metadata.PersonaId
            : null;

        var personas = await personaManager.GetAllAsync(cancellationToken);

        model.Personas = personas
            .Where(persona => !string.IsNullOrWhiteSpace(persona.Name))
            .Select(persona => new AIPersonaOption
            {
                Id = persona.Name,
                Text = string.IsNullOrWhiteSpace(persona.DisplayText)
                    ? persona.Name
                    : persona.DisplayText,
                Description = persona.Description,
                VoiceName = persona.VoiceName,
            })
            .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A link to a persona that no longer exists stays visible, so saving the editor does not drop it silently.
        if (!string.IsNullOrWhiteSpace(model.PersonaId) &&
            !model.Personas.Any(option => string.Equals(option.Id, model.PersonaId, StringComparison.OrdinalIgnoreCase)))
        {
            model.Personas.Insert(0, new AIPersonaOption
            {
                Id = model.PersonaId,
                Text = model.PersonaId,
            });
        }
    }

    /// <summary>
    /// Links <paramref name="entity"/> to the persona named <paramref name="personaId"/>, or removes the link when
    /// it is empty. Link parameters are kept.
    /// </summary>
    /// <param name="entity">The entity to link.</param>
    /// <param name="personaId">The persona's technical name, or an empty value for none.</param>
    public static void SetPersona(this ExtensibleEntity entity, string personaId)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var value = string.IsNullOrWhiteSpace(personaId)
            ? null
            : personaId.Trim();

        entity.Alter<AIPersonaMetadata>(metadata =>
        {
            metadata.PersonaId = value;
        });
    }
}
