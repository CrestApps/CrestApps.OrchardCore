using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Protects the secrets a recipe step or an API call gives a record in clear text, with the purpose
/// <see cref="IngestionSourceSecretOptions"/> names for their metadata object, as the record's editor protects them.
/// </summary>
/// <remarks>
/// A secret is protected with a purpose shared by every record of its kind, and the protected value is not bound to a
/// record or an address. A caller who read the protected value of one record could otherwise give it to another record
/// whose address they chose, and the stored key would be sent there. So a value that already reads back under the
/// purpose is refused where the caller is not trusted (the API), and kept where it is (a recipe). A value equal to the
/// one the record stores in the same place is left as it is: it is the stored secret given back, and the rule that drops
/// a kept secret when the record's address changes applies to it.
/// </remarks>
public static class IngestionSourceSecretProtection
{
    /// <summary>
    /// Protects the clear-text secrets in a record's incoming properties, changing them in place.
    /// </summary>
    /// <param name="recordType">The record type.</param>
    /// <param name="incoming">The record's incoming properties: each metadata object by its key.</param>
    /// <param name="stored">The record's stored properties, or <see langword="null"/> for a new record.</param>
    /// <param name="options">The purposes of the metadata objects.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="refuseProtected">Whether a value that already reads back under the purpose is refused rather than kept.</param>
    /// <returns>The members refused, as <c>Metadata.Member</c>; empty when none was.</returns>
    public static IReadOnlyList<string> Protect(
        Type recordType,
        JsonObject incoming,
        JsonObject stored,
        IngestionSourceSecretOptions options,
        IDataProtectionProvider dataProtectionProvider,
        bool refuseProtected)
    {
        ArgumentNullException.ThrowIfNull(recordType);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        var refused = new List<string>();

        foreach (var (name, value) in incoming ?? [])
        {
            if (value is JsonObject metadata && options.GetPurpose(recordType, name) is { } purpose)
            {
                Protect(metadata, Find(stored, name) as JsonObject, dataProtectionProvider, purpose, refuseProtected, name, refused);
            }
        }

        return refused;
    }

    private static void Protect(JsonObject incoming, JsonObject stored, IDataProtectionProvider dataProtectionProvider, string purpose, bool refuseProtected, string path, List<string> refused)
    {
        foreach (var (name, value) in incoming.ToList())
        {
            if (value is JsonObject child)
            {
                Protect(child, Find(stored, name) as JsonObject, dataProtectionProvider, purpose, refuseProtected, path + "." + name, refused);

                continue;
            }

            if (!IngestionSourceSecrets.IsSecret(name) || Text(value) is not { } text || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (string.Equals(text, Text(Find(stored, name)), StringComparison.Ordinal))
            {
                continue;
            }

            if (IsProtected(dataProtectionProvider, purpose, text))
            {
                if (refuseProtected)
                {
                    refused.Add(path + "." + name);
                }

                continue;
            }

            incoming[name] = dataProtectionProvider.ProtectSetting(purpose, text.Trim());
        }
    }

    private static bool IsProtected(IDataProtectionProvider dataProtectionProvider, string purpose, string value)
    {
        try
        {
            dataProtectionProvider.UnprotectSetting(purpose, value);

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static JsonNode Find(JsonObject node, string name)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var (key, value) in node)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string Text(JsonNode node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
