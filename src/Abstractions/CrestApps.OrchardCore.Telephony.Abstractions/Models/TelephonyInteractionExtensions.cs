using CrestApps.Core.Entities;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Reads and writes the typed aspects stored alongside a <see cref="TelephonyInteraction"/>.
/// </summary>
public static class TelephonyInteractionExtensions
{
    /// <summary>
    /// Tries to read the stored aspect.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <param name="aspect">The stored aspect when one is present; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the aspect was read.</returns>
    public static bool TryGet<T>(this TelephonyInteraction interaction, out T aspect)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return JsonPropertyBag.TryGet(interaction.Properties, out aspect);
    }

    /// <summary>
    /// Gets the stored aspect, or a new instance when the interaction does not carry one.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The stored aspect, or a new instance.</returns>
    public static T GetOrCreate<T>(this TelephonyInteraction interaction)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return JsonPropertyBag.GetOrCreate<T>(interaction.Properties);
    }

    /// <summary>
    /// Stores the aspect, replacing any previous value.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="interaction">The interaction.</param>
    /// <param name="aspect">The aspect to store.</param>
    /// <returns>The interaction, for chaining.</returns>
    public static TelephonyInteraction Put<T>(this TelephonyInteraction interaction, T aspect)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        interaction.Properties ??= [];
        JsonPropertyBag.Put(interaction.Properties, aspect);

        return interaction;
    }
}
