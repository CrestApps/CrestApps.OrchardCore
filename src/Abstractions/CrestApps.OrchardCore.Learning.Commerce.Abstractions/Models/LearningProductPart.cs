using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Commerce.Models;

/// <summary>
/// What buying a course or a learning path product grants: the item itself, other courses (a bundle), or seats for an
/// account, each with an access length.
/// </summary>
public sealed class LearningProductPart : ContentPart
{
    /// <summary>
    /// Gets or sets the grants; empty grants the course or path the part is on, for oneself.
    /// </summary>
    public List<LearningProductGrant> Grants { get; set; } = [];
}

/// <summary>
/// One thing a purchase grants.
/// </summary>
public sealed class LearningProductGrant
{
    /// <summary>
    /// Gets or sets what is granted.
    /// </summary>
    public LearningProductGrantKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the course or path; empty for the item the part is on.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets how many days access lasts; empty for good.
    /// </summary>
    public int? AccessDays { get; set; }

    /// <summary>
    /// Gets or sets how many seats one unit buys, for <see cref="LearningProductGrantKind.Seats"/>.
    /// </summary>
    public int Seats { get; set; } = 1;
}

/// <summary>
/// What a purchase grants.
/// </summary>
public enum LearningProductGrantKind
{
    /// <summary>
    /// An enrollment of the buyer in a course.
    /// </summary>
    Course,

    /// <summary>
    /// The buyer starts a learning path.
    /// </summary>
    LearningPath,

    /// <summary>
    /// A licence pool of seats in a course for the buyer's account.
    /// </summary>
    Seats,
}
