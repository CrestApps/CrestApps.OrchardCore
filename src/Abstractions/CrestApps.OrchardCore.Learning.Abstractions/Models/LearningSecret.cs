using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The hash of a secret a learner types, such as the enrollment key of a course or the access code of a quiz. It is kept
/// out of the content item it belongs to, so the content API, content exports and templates that print the item never
/// carry it.
/// </summary>
public sealed class LearningSecret : CatalogItem
{
    /// <summary>
    /// Gets or sets the kind of secret, such as <c>EnrollmentKey</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the content item the secret belongs to.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the salted hash of the secret.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets when the secret was last set, in UTC.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
