using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// What a digital credential says its holder achieved, and the rules that award it. The image is the media field
/// <c>Image</c> of the credential definition type's own part.
/// </summary>
public sealed class CredentialDefinitionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the description of the achievement, as plain text.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the criteria narrative: what a person did to earn the credential.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the Open Badges 3.0 achievement type, such as Badge or Certificate.
    /// </summary>
    public string AchievementType { get; set; }

    /// <summary>
    /// Gets or sets the tags of the achievement.
    /// </summary>
    public IList<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the alignments of the achievement to outside frameworks.
    /// </summary>
    public IList<CredentialAlignment> Alignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the Skills skill the achievement aligns to, when Skills is on.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the id of the Skills certification the achievement aligns to, when Skills is on.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets how many months an award stays valid, or <see langword="null"/> when it does not expire.
    /// </summary>
    public int? ValidityMonths { get; set; }

    /// <summary>
    /// Gets or sets the rules that award the credential. Any one of them awards it.
    /// </summary>
    public IList<CredentialAwardRule> Rules { get; set; } = [];
}
