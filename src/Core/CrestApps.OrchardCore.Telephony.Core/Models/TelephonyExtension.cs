using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Models;

/// <summary>
/// Represents an internal extension: a stable, tenant-scoped number that maps to an on-platform user. It is the
/// provider-neutral identity that lets one agent call another by extension without either party knowing the
/// other's ephemeral provider endpoint. Providers translate the resolved user into their own live endpoint.
/// </summary>
public sealed class TelephonyExtension : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the extension entry (used for admin display and catalog identity).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the dialed extension number, for example <c>1001</c>. It is unique per tenant.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user this extension rings.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the Orchard user this extension rings.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to a colleague who calls this extension.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the extension was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Normalizes a dialed extension number to its canonical form for storage and lookup: trimmed and
    /// lower-cased so matching is case-insensitive. Returns <see langword="null"/> for a blank value.
    /// </summary>
    /// <param name="number">The raw extension number.</param>
    /// <returns>The normalized number, or <see langword="null"/>.</returns>
    public static string NormalizeNumber(string number)
        => string.IsNullOrWhiteSpace(number) ? null : number.Trim().ToLowerInvariant();
}
