namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// The export options of a lead type, stored on the export entry.
/// </summary>
public sealed class LeadExportOptionsPart
{
    /// <summary>
    /// Gets or sets whether converted leads are left out of the export.
    /// </summary>
    public bool ExcludeConvertedLeads { get; set; } = true;
}
