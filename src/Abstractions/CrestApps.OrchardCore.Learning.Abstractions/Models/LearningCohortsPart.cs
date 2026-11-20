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
}
