using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Models;

/// <summary>
/// A person's progress on a learning path.
/// </summary>
public sealed class LearningPathProgress : CatalogItem
{
    /// <summary>
    /// Gets or sets the content item id of the path.
    /// </summary>
    public string PathId { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public LearningPathState State { get; set; }

    /// <summary>
    /// Gets or sets the index of the last stage opened to the person.
    /// </summary>
    public int OpenedStage { get; set; }

    /// <summary>
    /// Gets or sets the ids of the path's courses the person completed when the progress was last evaluated.
    /// </summary>
    public List<string> CompletedCourseIds { get; set; } = [];

    /// <summary>
    /// Gets or sets what started the path, such as required learning.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the record in <see cref="Source"/>.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the courses of the path are due.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the person started the path.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the person completed it.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets who started it.
    /// </summary>
    public string StartedBy { get; set; }
}

/// <summary>
/// The state of a person's progress on a learning path.
/// </summary>
public enum LearningPathState
{
    /// <summary>
    /// The person is working through it.
    /// </summary>
    InProgress,

    /// <summary>
    /// The person completed it.
    /// </summary>
    Completed,
}
