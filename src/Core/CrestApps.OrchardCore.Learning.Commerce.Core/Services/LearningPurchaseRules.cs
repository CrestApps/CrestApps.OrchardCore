using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Services;

/// <summary>
/// The rules the cart and the fulfilment share about what a purchase may grant.
/// </summary>
public static class LearningPurchaseRules
{
    /// <summary>
    /// Says whether a course lets a learner in only with its enrollment key or a staff approval. A purchase asks for
    /// neither, so such a course is sold only by a product that says it enrolls without them.
    /// </summary>
    /// <param name="course">The course content item.</param>
    /// <returns><see langword="true"/> for a course in the Key or Approval enrollment mode.</returns>
    public static bool NeedsKeyOrApproval(ContentItem course)
    {
        ArgumentNullException.ThrowIfNull(course);

        return course.TryGet<CoursePart>(out var part) && part.EnrollmentMode is EnrollmentMode.Key or EnrollmentMode.Approval;
    }
}
