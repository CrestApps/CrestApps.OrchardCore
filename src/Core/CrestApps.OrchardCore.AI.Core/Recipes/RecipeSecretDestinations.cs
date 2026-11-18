using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Applies the editors' rule for stored secrets to a recipe or an API call that updates an AI connection, deployment,
/// tool instance, data source, file source or web crawler: an update that leaves a secret out keeps the stored one, so when it also points the entry at another
/// address, or makes it sign in another way, the secrets it kept are dropped and must be given again.
/// </summary>
/// <remarks>
/// An address is any text member whose name ends with <c>Endpoint</c> or <c>Url</c>, compared by scheme, host and port
/// with <see cref="SecretDestination"/>; a member whose name ends with <c>AuthenticationType</c>, <c>EnvironmentType</c>
/// or <c>CloudId</c> is compared as it is. A connection addressed by host rather than by URL, such as FTP or SFTP, is
/// an object with a <c>Host</c> member; in it, the host, port and user name, the SFTP host key fingerprint and the
/// proxy's type, host, port and user name are compared as they are, with <see cref="SecretDestination.SameValue"/>, as
/// the FTP and SFTP editors compare them. A secret is a text member <see cref="IngestionSourceSecrets.IsSecret"/> names,
/// in <see cref="ExtensibleEntity.Properties"/>, or the <c>DefaultValue</c> of a tool instance's declared parameter marked
/// <c>IsSecret</c>, matched with the stored parameter by its <c>Name</c>. Matching by name covers the metadata of every
/// provider without listing it, as the deployment plans do.
/// </remarks>
public static class RecipeSecretDestinations
{
    private const string HostMember = "Host";

    private const string NameMember = "Name";

    private const string IsSecretMember = "IsSecret";

    private const string DefaultValueMember = "DefaultValue";

    private static readonly string[] _addressSuffixes = ["Endpoint", "Url"];

    private static readonly string[] _exactSuffixes = ["AuthenticationType", "EnvironmentType", "CloudId"];

    // The members of a connection addressed by host that say which server and account a secret is used with.
    private static readonly string[] _serverMembers = [HostMember, "Port", "Username", "HostKeyFingerprint", "ProxyType", "ProxyHost", "ProxyPort", "ProxyUsername"];

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
    /// Updates an entry from JSON, as a recipe step or an API endpoint does, and drops the secrets the update kept when
    /// it also changed where the entry sends them. When one is dropped the entry is saved again, so the store keeps the
    /// entry without it even when the first save already wrote the entry.
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <param name="data">The JSON to apply.</param>
    /// <param name="update">Applies JSON to the entry and saves it, such as the entry's catalog manager update; called
    /// with <see langword="null"/> to save the entry again as it is.</param>
    public static async ValueTask UpdateAsync(ExtensibleEntity entry, JsonNode data, Func<JsonNode, ValueTask> update)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(update);

        var before = Snapshot(entry);

        await update(data);

        if (DropKeptSecrets(entry, before))
        {
            await update(null);
        }
    }

    /// <summary>
    /// Updates an entry from JSON, saving it once, without the secrets the update would keep although it also changes
    /// where the entry sends them. The JSON is first applied to a new entry, which is not saved; when that drops a secret,
    /// the JSON carries the new entry's properties without it, so no save, and nothing a save queues, such as a data
    /// source's sync, ever holds the new destination with the kept secret (review 9, K1).
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <param name="data">The JSON to apply, in the shape the entry's catalog handlers read, which replace the properties
    /// with the ones it gives.</param>
    /// <param name="preview">Makes a new, unsaved entry from JSON, such as the entry's catalog manager <c>NewAsync</c>.</param>
    /// <param name="update">Applies JSON to the entry and saves it, such as the entry's catalog manager update.</param>
    public static async ValueTask UpdateAsync(ExtensibleEntity entry, JsonObject data, Func<JsonObject, ValueTask<ExtensibleEntity>> preview, Func<JsonNode, ValueTask> update)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(preview);

        var before = Snapshot(entry);
        var candidate = await preview(Apply(before, data));

        if (candidate is not null)
        {
            DropKeptSecrets(data, candidate, before);
        }

        await UpdateAsync(entry, data, update);
    }

    /// <summary>
    /// Drops, from the JSON an update is about to save, the secrets it would keep although it also changes where the entry
    /// sends them: when they are dropped from the candidate, the JSON's properties are replaced with the candidate's.
    /// </summary>
    /// <param name="data">The JSON about to be saved; the catalog handlers replace the entry's properties with its own.</param>
    /// <param name="candidate">The entry as the update would leave it, not saved.</param>
    /// <param name="before">The stored entry's JSON, from <see cref="Snapshot"/>.</param>
    /// <returns><see langword="true"/> when a secret was dropped.</returns>
    public static bool DropKeptSecrets(JsonObject data, ExtensibleEntity candidate, JsonObject before)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!DropKeptSecrets(candidate, before))
        {
            return false;
        }

        Member(data, nameof(ExtensibleEntity.Properties), out var key);
        data[key ?? nameof(ExtensibleEntity.Properties)] =Snapshot(candidate)[nameof(ExtensibleEntity.Properties)]?.DeepClone();

        return true;
    }

    // The entry's JSON with each member the update gives in place of its own, as the catalog handlers apply it.
    private static JsonObject Apply(JsonObject before, JsonObject data)
    {
        var result = before.DeepClone().AsObject();

        foreach (var (key, value) in data)
        {
            Member(result, key, out var existing);

            if (existing is not null)
            {
                result.Remove(existing);
            }

            result[key] = value?.DeepClone();
        }

        return result;
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
            else if (value is JsonArray items && DropKeptParameterValues(items, before[key] as JsonArray))
            {
                dropped = true;
            }
        }

        return dropped;
    }

    // A tool instance's declared parameters are an array of objects named by "Name"; a fixed one marked "IsSecret", such as
    // a key sent in a header, holds its protected value in "DefaultValue", which is dropped when the recipe kept it.
    private static bool DropKeptParameterValues(JsonArray after, JsonArray before)
    {
        if (before is null)
        {
            return false;
        }

        var dropped = false;

        foreach (var parameter in after.OfType<JsonObject>())
        {
            if (Member(parameter, IsSecretMember, out _) is not JsonValue isSecret ||
                !isSecret.TryGetValue<bool>(out var secret) ||
                !secret ||
                Member(parameter, DefaultValueMember, out var valueKey) is not { } value ||
                Text(value) is not { Length: > 0 } text)
            {
                continue;
            }

            var name = Text(Member(parameter, NameMember, out _));
            var previous = before.OfType<JsonObject>().FirstOrDefault(item => string.Equals(Text(Member(item, NameMember, out _)), name, StringComparison.OrdinalIgnoreCase));

            if (previous is not null && string.Equals(text, Text(Member(previous, DefaultValueMember, out _)), StringComparison.Ordinal))
            {
                parameter[valueKey] = null;
                dropped = true;
            }
        }

        return dropped;
    }

    private static JsonNode Member(JsonObject node, string name, out string key)
    {
        foreach (var (memberName, value) in node)
        {
            if (string.Equals(memberName, name, StringComparison.OrdinalIgnoreCase))
            {
                key = memberName;

                return value;
            }
        }

        key = null;

        return null;
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
                ? SecretDestination.SameValue(was, now)
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
        var addressedByHost = node.Any(member => string.Equals(member.Key, HostMember, StringComparison.OrdinalIgnoreCase));

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
            else if ((EndsWithAny(name, _exactSuffixes) || (addressedByHost && IsServerMember(name))) && value is JsonValue && value.ToString() is { Length: > 0 } exact)
            {
                result[path + "*"] = exact;
            }
        }
    }

    private static bool IsServerMember(string name)
        => _serverMembers.Contains(name, StringComparer.OrdinalIgnoreCase);

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
