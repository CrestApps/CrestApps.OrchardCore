namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the outcome of a provider directory lookup.
/// </summary>
public sealed class TelephonyDirectoryResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the directory lookup succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the directory entries.
    /// </summary>
    public IReadOnlyList<TelephonyDirectoryEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets a provider-neutral error message when the lookup fails.
    /// </summary>
    public string Error { get; set; }
}
