using CrestApps.Core.Entities;

namespace CrestApps.Core.ContactCenter.Models;

/// <summary>
/// Reads and writes the typed aspects stored alongside an <see cref="Interaction"/>.
/// </summary>
public static class InteractionExtensions
{
    /// <summary>
    /// Tries to read the stored aspect.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <param name="aspect">The stored aspect when one is present; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the aspect was read.</returns>
    public static bool TryGet<T>(this Interaction interaction, out T aspect)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return JsonPropertyBag.TryGet(interaction.EntityProperties, out aspect);
    }

    /// <summary>
    /// Gets the stored aspect, or a new instance when the interaction does not carry one.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The stored aspect, or a new instance.</returns>
    public static T GetOrCreate<T>(this Interaction interaction)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return JsonPropertyBag.GetOrCreate<T>(interaction.EntityProperties);
    }

    /// <summary>
    /// Stores the aspect, replacing any previous value.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <param name="aspect">The aspect to store.</param>
    /// <returns>The interaction, for chaining.</returns>
    public static Interaction Put<T>(this Interaction interaction, T aspect)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.EntityProperties ??= [];
        JsonPropertyBag.Put(interaction.EntityProperties, aspect);

        return interaction;
    }
}
