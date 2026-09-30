namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the settings for <see cref="LeadPart"/> on a lead content type.
/// </summary>
public sealed class LeadPartSettings
{
    /// <summary>
    /// Gets or sets the contact content type a lead of this type becomes when it is converted.
    /// </summary>
    public string TargetContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the opportunity content type offered first when a conversion creates an opportunity.
    /// </summary>
    public string DefaultOpportunityContentType { get; set; }
}
