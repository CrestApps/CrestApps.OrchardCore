namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the settings for <see cref="OpportunityPart"/> on an opportunity content type.
/// </summary>
public sealed class OpportunityPartSettings
{
    /// <summary>
    /// Gets or sets the identifiers of the stages an opportunity of this type moves through. When empty, every
    /// stage in the catalog applies.
    /// </summary>
    public string[] StageIds { get; set; } = [];
}
