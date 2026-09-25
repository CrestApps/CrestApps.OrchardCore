namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// An extension of the phone system and the person it rings, as the soft phone shows it.
/// </summary>
public sealed class TelephonyExtensionDirectoryEntry
{
    /// <summary>
    /// Gets or sets the extension number.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Gets or sets the name to show for the person the extension rings: their display name, else their username,
    /// else the extension itself.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the username of the person the extension rings.
    /// </summary>
    public string UserName { get; set; }
}

/// <summary>
/// The phone system's extensions, for the soft phone to name the people it calls and transfers to.
/// </summary>
public sealed class TelephonyExtensionDirectoryResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the extensions were read.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the extensions.
    /// </summary>
    public IReadOnlyList<TelephonyExtensionDirectoryEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets why the extensions could not be read.
    /// </summary>
    public string Error { get; set; }
}
