using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Applies the editors' rule for stored secrets to a recipe that updates an AI connection, deployment, tool instance or
/// data source: a recipe that leaves a secret out keeps the stored one, so when it also points the entry at another
/// address, or makes it sign in another way, the secrets it kept are dropped and must be given again.
/// </summary>
/// <remarks>
/// An address is any text member whose name ends with <c>Endpoint</c> or <c>Url</c>, compared by scheme, host and port
/// with <see cref="SecretDestination"/>; a member whose name ends with <c>AuthenticationType</c>, <c>EnvironmentType</c>
/// or <c>CloudId</c> is compared as it is. A secret is a text member <see cref="IngestionSourceSecrets.IsSecret"/> names,
/// in <see cref="ExtensibleEntity.Properties"/>. Matching by name covers the metadata of every provider without listing
/// it, as the deployment plans do.
/// </remarks>
public static class RecipeSecretDestinations
{
    private static readonly string[] _addressSuffixes = ["Endpoint", "Url"];

    private static readonly string[] _exactSuffixes = ["AuthenticationType", "EnvironmentType", "CloudId"];

    /// <summary>
    /// Takes the JSON of an entry before a recipe changes it.
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <returns>The entry's JSON.</returns>
    public static JsonObject Snapshot(ExtensibleEntity entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return JsonSerializer.SerializeToNode(entry, entry.GetType()) as JsonObject ?? [];
    }

    /// <summary>
    /// Drops the secrets a recipe kept from before when it changed where the entry sends them.
    /// </summary>
    /// <param name="entry">The entry, after the recipe applied its JSON.</param>
    /// <param name="before">The entry's JSON before, from <see cref="Snapshot"/>.</param>
    /// <returns><see langword="true"/> when a secret was dropped.</returns>
    public static bool DropKeptSecrets(ExtensibleEntity entry, JsonObject before)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(before);

        var after = Snapshot(entry);

        if (!Moved(before, after))
        {
            return false;
        }

        var beforeProperties = before[nameof(ExtensibleEntity.Properties)] as JsonObject;
        var afterProperties = after[nameof(ExtensibleEntity.Properties)] as JsonObject;

        if (beforeProperties is null || afterProperties is null || entry.Properties is null)
        {
            return false;
        }

        var dropped = false;

        foreach (var (key, value) in afterProperties.ToList())
        {
            if (value is JsonValue && IsKeptSecret(key, value, beforeProperties[key]))
            {
                entry.Properties.Remove(key);
                dropped = true;
            }
            else if (value is JsonObject child && DropKept(child, beforeProperties[key] as JsonObject))
            {
                entry.Properties[key] = child;
                dropped = true;
            }
        }

        return dropped;
    }

    private static bool DropKept(JsonObject after, JsonObject before)
    {
        if (before is null)
        {
            return false;
        }

        var dropped = false;

        foreach (var (key, value) in after.ToList())
        {
            if (value is JsonValue && IsKeptSecret(key, value, before[key]))
            {
                after[key] = null;
                dropped = true;
            }
            else if (value is JsonObject child && DropKept(child, before[key] as JsonObject))
            {
                dropped = true;
            }
        }

        return dropped;
    }

    private static bool IsKeptSecret(string name, JsonNode after, JsonNode before)
        => IngestionSourceSecrets.IsSecret(name) &&
            Text(after) is { Length: > 0 } value &&
            string.Equals(value, Text(before), StringComparison.Ordinal);

    private static bool Moved(JsonObject before, JsonObject after)
    {
        var stored = Destinations(before);
        var current = Destinations(after);

        foreach (var path in stored.Keys.Union(current.Keys, StringComparer.Ordinal))
        {
            stored.TryGetValue(path, out var was);
            current.TryGetValue(path, out var now);

            var same = path.EndsWith('*')
                ? string.Equals(was?.Trim(), now?.Trim(), StringComparison.OrdinalIgnoreCase)
                : SecretDestination.SameOrigin(was, now);

            if (!same)
            {
                return true;
            }
        }

        return false;
    }

    // Each address or authentication member by its path; an exact-compare path ends with "*".
    private static Dictionary<string, string> Destinations(JsonObject root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        Collect(root, string.Empty, result);

        return result;
    }

    private static void Collect(JsonObject node, string prefix, Dictionary<string, string> result)
    {
        foreach (var (name, value) in node)
        {
            var path = prefix + "/" + name.ToUpperInvariant();

            if (value is JsonObject child)
            {
                Collect(child, path, result);
            }
            else if (EndsWithAny(name, _addressSuffixes) && Text(value) is { Length: > 0 } address)
            {
                result[path] = address;
            }
            else if (EndsWithAny(name, _exactSuffixes) && value is JsonValue && value.ToString() is { Length: > 0 } exact)
            {
                result[path + "*"] = exact;
            }
        }
    }

    private static bool EndsWithAny(string name, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Text(JsonNode node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
