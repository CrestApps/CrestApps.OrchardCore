using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Finds the secrets in the extension data of an ingestion source, such as a file source's FTP password or a crawler's
/// access token, so a deployment plan never carries them and a recipe that leaves them out keeps the stored ones.
/// </summary>
/// <remarks>
/// A member is a secret when it holds text and its name ends with one of <see cref="SecretSuffixes"/>, ignoring case,
/// wherever it sits in the extension data. Matching by name keeps connection metadata added by other modules covered without listing it.
/// Secrets are protected with the keys of the site that stored them, so they could not be read on another site anyway.
/// </remarks>
public static class IngestionSourceSecrets
{
    /// <summary>
    /// The endings of the member names that hold a secret.
    /// </summary>
    public static readonly IReadOnlyList<string> SecretSuffixes =
    [
        "Password",
        "Passphrase",
        "PrivateKey",
        "Secret",
        "ApiKey",
        "AccessKey",
        "Token",
        "Certificate",
        "ConnectionString",
    ];

    /// <summary>
    /// Determines whether a member name holds a secret.
    /// </summary>
    /// <param name="name">The member name.</param>
    /// <returns><see langword="true"/> when the member holds a secret.</returns>
    public static bool IsSecret(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var suffix in SecretSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes every secret member from a JSON tree, at any depth.
    /// </summary>
    /// <param name="node">The JSON to clean, changed in place.</param>
    public static void Remove(JsonNode node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var name in json.Where(member => IsSecretValue(member.Key, member.Value)).Select(member => member.Key).ToArray())
                {
                    json.Remove(name);
                }

                foreach (var (_, value) in json)
                {
                    Remove(value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Remove(item);
                }

                break;
        }
    }

    /// <summary>
    /// Copies into an incoming JSON tree the stored secrets it leaves out or leaves empty, at any depth, so updating a
    /// record from a recipe that carries no secret keeps the one it has.
    /// </summary>
    /// <param name="incoming">The JSON the recipe carries, changed in place.</param>
    /// <param name="stored">The JSON of the stored record.</param>
    public static void KeepStored(JsonObject incoming, JsonObject stored)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        if (stored is null)
        {
            return;
        }

        foreach (var (name, value) in stored)
        {
            if (IsSecretValue(name, value))
            {
                if (incoming[name] is null || (incoming[name] is JsonValue text && text.TryGetValue<string>(out var current) && string.IsNullOrEmpty(current)))
                {
                    incoming[name] = value?.DeepClone();
                }

                continue;
            }

            if (value is JsonObject child && incoming[name] is JsonObject incomingChild)
            {
                KeepStored(incomingChild, child);
            }
        }
    }

    // A secret is text: a flag such as ValidateAnyCertificate ends like a secret but is a setting.
    private static bool IsSecretValue(string name, JsonNode value)
        => IsSecret(name) && value is JsonValue text && text.GetValueKind() == JsonValueKind.String;
}
