using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// What saving the activity editor is allowed to change about how the activity is carried out.
/// </summary>
internal static class OmnichannelActivityEditRules
{
    /// <summary>
    /// Whether the subject's flow settings -- channel, endpoint, interaction type and campaign -- are written onto
    /// the activity by this save.
    /// </summary>
    /// <remarks>
    /// They are how a new activity, or one moved to a different subject, learns how it is to be carried out. An
    /// existing activity already knows, and the editor has no fields for any of it: re-applying them on every save
    /// replaced an automated call's settings with the subject's defaults, and one rescheduled from the editor became
    /// a manual task with no channel that nothing would ever place.
    /// </remarks>
    /// <param name="activity">The activity as it stood before this save.</param>
    /// <param name="subjectContentType">The subject chosen in the editor.</param>
    /// <param name="isNew">Whether the activity is being created.</param>
    public static bool AppliesFlowDelivery(OmnichannelActivity activity, string subjectContentType, bool isNew)
        => isNew ||
            activity is null ||
            !string.Equals(activity.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase);
}
