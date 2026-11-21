using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// A published credential definition as the award service reads it.
/// </summary>
public sealed class CredentialDefinition
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the content item version id of the published version.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the achievement, without its address, which the award service sets.
    /// </summary>
    public CredentialAchievement Achievement { get; set; }

    /// <summary>
    /// Gets or sets the award rules.
    /// </summary>
    public IList<CredentialAwardRule> Rules { get; set; } = [];
}
