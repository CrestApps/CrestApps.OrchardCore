namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// Options for omnichannel contact import behavior.
/// Stored in the <see cref="ContentTransfer.ContentTransferEntry"/> Properties bag.
/// </summary>
public sealed class OmnichannelContactImportOptionsPart
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore duplicate contacts based on phone number.
    /// When enabled, only the first row with a given phone number is imported.
    /// </summary>
    public bool IgnoreDuplicateByPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore phone numbers listed on a national do-not-call registry.
    /// </summary>
    public bool IgnoreDoNotCallNumbers { get; set; }

    /// <summary>
    /// Gets or sets the registry keys selected for DNC checking during this import.
    /// </summary>
    public string[] SelectedRegistryKeys { get; set; } = [];
}
