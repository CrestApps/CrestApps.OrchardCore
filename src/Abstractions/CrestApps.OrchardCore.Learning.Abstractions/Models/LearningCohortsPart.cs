using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The cohorts a course or a learning path is linked to. The people in them are enrolled while they are in them; it is
/// kept on the content item, so recipes and deployments carry it with the course. The type does not need the part: the
/// Cohorts card of the editor adds it to items whose type has a part an <c>ICohortLearningTarget</c> handles.
/// </summary>
public sealed class LearningCohortsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ids of the linked cohorts.
    /// </summary>
    public List<string> CohortIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the user id of the person who last saved the cohorts. Linking a cohort enrolls everyone in it, so
    /// the sync applies the cohorts only when this person (or the person who publishes the item) may manage enrollments.
    /// It is written by the editor, by the Moodle import and on every save made in a request, never taken from the
    /// posted content.
    /// </summary>
    public string LinkedBy { get; set; }
}
