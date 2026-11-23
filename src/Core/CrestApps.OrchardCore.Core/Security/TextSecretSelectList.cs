using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// Builds the picker of Orchard Core text secrets a settings editor shows when a module keeps an outside service's
/// credential (an API key, a client secret) in Orchard Core Secrets.
/// </summary>
public static class TextSecretSelectList
{
    /// <summary>
    /// Returns the tenant's text secrets as select list items sorted by name, with <paramref name="selected"/> picked.
    /// A selected name that has no secret yet is listed as "(not created yet)" so the setting still shows.
    /// </summary>
    /// <param name="secretManager">The tenant's secret manager.</param>
    /// <param name="selected">The secret name the setting holds now.</param>
    /// <param name="localizer">The caller's localizer, for the "not created yet" text.</param>
    public static async Task<List<SelectListItem>> BuildAsync(ISecretManager secretManager, string selected, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(secretManager);
        ArgumentNullException.ThrowIfNull(localizer);

        var items = new List<SelectListItem>();

        foreach (var info in await secretManager.GetSecretInfosAsync())
        {
            if (IsTextSecret(info.Type))
            {
                items.Add(new SelectListItem(info.Name, info.Name, string.Equals(info.Name, selected, StringComparison.Ordinal)));
            }
        }

        // A name typed earlier, or the default, shows even before the secret exists.
        if (!string.IsNullOrEmpty(selected) && !items.Any(item => item.Value == selected))
        {
            items.Add(new SelectListItem(localizer["{0} (not created yet)", selected], selected, selected: true));
        }

        return items.OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Returns whether a secret's stored type is a text secret. The store records the full type name
    /// (<c>OrchardCore.Secrets.TextSecret</c>); the short name is accepted too.
    /// </summary>
    /// <param name="type">The type the secret store recorded.</param>
    public static bool IsTextSecret(string type)
        => string.Equals(type, typeof(TextSecret).FullName, StringComparison.Ordinal)
            || string.Equals(type, nameof(TextSecret), StringComparison.Ordinal);
}
