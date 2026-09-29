using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Moves a settings object that holds data-protected secrets between environments without ever writing a secret into
/// the plan.
/// </summary>
/// <remarks>
/// A protected value is encrypted with the data protection keys of the tenant that stored it, so it cannot be read by
/// any other environment; exporting it would carry a blob the destination cannot decrypt, and exporting it decrypted
/// would put a credential in a file that gets attached to tickets and committed to repositories. The export therefore
/// leaves every protected member out. The import applies the members it is given and leaves the destination's stored
/// secret untouched when the plan carries none, so replaying an exported plan never wipes a credential the destination
/// already holds. A hand-written recipe may still supply a secret, in clear text, and it is protected with the same
/// purpose the settings screen uses before it is stored.
/// </remarks>
public static class ProtectedSettingsDeploymentSerializer
{
    /// <summary>
    /// Serializes the settings into the JSON a deployment plan carries, without any protected member.
    /// </summary>
    /// <param name="settings">The settings to export.</param>
    /// <param name="protectedMembers">The names of the members that hold protected values.</param>
    /// <returns>A JSON object holding every other member of the settings.</returns>
    public static JsonObject Export(object settings, IEnumerable<string> protectedMembers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(protectedMembers);

        var node = CatalogDeploymentSerializer.Export(settings);

        foreach (var member in protectedMembers)
        {
            node.Remove(member);
        }

        return node;
    }

    /// <summary>
    /// Applies the members present in <paramref name="data"/> onto <paramref name="settings"/>, protecting any secret
    /// the data supplies in clear text.
    /// </summary>
    /// <param name="settings">The stored settings to update.</param>
    /// <param name="data">The JSON carried by the recipe step.</param>
    /// <param name="protectorPurposes">The data protection purpose of each member that holds a protected value.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect supplied secrets.</param>
    public static void Populate(
        object settings,
        JsonObject data,
        IReadOnlyDictionary<string, string> protectorPurposes,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(protectorPurposes);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (data is null)
        {
            return;
        }

        var plain = data.DeepClone().AsObject();

        foreach (var member in protectorPurposes.Keys)
        {
            plain.Remove(member);
        }

        CatalogDeploymentSerializer.Populate(settings, plain);

        var type = settings.GetType();

        foreach (var (member, purpose) in protectorPurposes)
        {
            var value = data[member] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                ? text?.Trim()
                : null;

            // An empty or missing secret means the plan does not carry one, never that the stored one should be erased.
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite || property.PropertyType != typeof(string))
            {
                throw new InvalidOperationException($"'{type.Name}.{member}' is not a writable string member and cannot hold a protected value.");
            }

            property.SetValue(settings, dataProtectionProvider.CreateProtector(purpose).Protect(value));
        }
    }
}
