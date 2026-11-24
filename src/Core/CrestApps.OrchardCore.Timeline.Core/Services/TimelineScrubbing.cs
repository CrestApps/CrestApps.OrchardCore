using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Rewrites the text values an entry keeps in its properties, for an erasure.
/// </summary>
public static class TimelineScrubbing
{
    /// <summary>
    /// Returns the properties with every text value passed through <paramref name="scrub"/>, or <see langword="null"/>
    /// when none changed. Values that aren't text (numbers, flags, objects) are kept as they are.
    /// </summary>
    /// <param name="properties">The entry's properties.</param>
    /// <param name="scrub">Gives the text to keep for a text.</param>
    /// <returns>The rewritten properties, or <see langword="null"/>.</returns>
    public static Dictionary<string, object> ScrubProperties(IDictionary<string, object> properties, Func<string, string> scrub)
    {
        ArgumentNullException.ThrowIfNull(scrub);

        if (properties is null || properties.Count == 0)
        {
            return null;
        }

        Dictionary<string, object> result = null;

        foreach (var (key, value) in properties)
        {
            var text = value switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonValue node when node.TryGetValue<string>(out var s) => s,
                _ => null,
            };

            if (text is null)
            {
                continue;
            }

            var scrubbed = scrub(text);

            if (scrubbed != text)
            {
                result ??= new Dictionary<string, object>(properties, StringComparer.Ordinal);
                result[key] = scrubbed;
            }
        }

        return result;
    }
}
