using CrestApps.Core.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Reads and writes the typed aspects stored in an <see cref="OmnichannelMessage"/>'s property bag.
/// </summary>
/// <remarks>
/// The key and the serialized shape match what Orchard Core's entity helpers produced, so messages
/// written before the message stopped deriving from the Orchard entity base still read back.
/// </remarks>
public static class OmnichannelMessageExtensions
{
    /// <summary>
    /// Gets the stored aspect, or a new instance when the message does not carry one.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="message">The message.</param>
    /// <returns>The stored aspect, or a new instance.</returns>
    public static T GetOrCreate<T>(this OmnichannelMessage message)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(message);

        return JsonPropertyBag.GetOrCreate<T>(message.Properties);
    }

    /// <summary>
    /// Tries to read the stored aspect.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="message">The message.</param>
    /// <param name="aspect">The stored aspect when one is present; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the aspect was read.</returns>
    public static bool TryGet<T>(this OmnichannelMessage message, out T aspect)
    {
        ArgumentNullException.ThrowIfNull(message);

        return JsonPropertyBag.TryGet(message.Properties, out aspect);
    }

    /// <summary>
    /// Stores the aspect, replacing any previous value.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="message">The message.</param>
    /// <param name="aspect">The aspect to store.</param>
    /// <returns>The message, for chaining.</returns>
    public static OmnichannelMessage Put<T>(this OmnichannelMessage message, T aspect)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.Properties ??= [];
        JsonPropertyBag.Put(message.Properties, aspect);

        return message;
    }
}
