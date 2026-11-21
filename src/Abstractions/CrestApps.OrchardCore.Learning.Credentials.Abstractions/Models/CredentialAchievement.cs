namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// The achievement of a credential definition as it was when it was awarded. An award keeps its own copy, so it never
/// depends on an old content version that Orchard Core may prune.
/// </summary>
public sealed class CredentialAchievement
{
    /// <summary>
    /// Gets or sets the achievement's own address.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description, as plain text.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the criteria narrative.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the absolute address of the image, when there is one.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the Open Badges 3.0 achievement type.
    /// </summary>
    public string AchievementType { get; set; }

    /// <summary>
    /// Gets or sets the tags.
    /// </summary>
    public IList<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the alignments.
    /// </summary>
    public IList<CredentialAlignment> Alignments { get; set; } = [];

    /// <summary>
    /// Gets or sets how many months an award stays valid, or <see langword="null"/> when it does not expire.
    /// </summary>
    public int? ValidityMonths { get; set; }
}
