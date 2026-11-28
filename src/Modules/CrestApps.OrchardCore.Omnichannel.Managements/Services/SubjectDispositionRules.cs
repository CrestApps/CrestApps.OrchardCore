using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Which dispositions an activity may be completed with, shared by the activity editor and the AI tools.
/// </summary>
internal static class SubjectDispositionRules
{
    /// <summary>
    /// Finds the disposition when the activity may be completed with it: one that a subject action of the activity's
    /// subject is configured for, or the disposition the activity already has.
    /// </summary>
    /// <param name="actionCatalog">The subject actions.</param>
    /// <param name="dispositionsCatalog">The dispositions.</param>
    /// <param name="activity">The activity being completed.</param>
    /// <param name="dispositionId">The chosen disposition.</param>
    /// <returns>The disposition, or <see langword="null"/> when it is not one the activity may take.</returns>
    public static async Task<OmnichannelDisposition> FindAllowedAsync(
        IReadCatalog<SubjectAction> actionCatalog,
        IReadCatalog<OmnichannelDisposition> dispositionsCatalog,
        OmnichannelActivity activity,
        string dispositionId)
    {
        ArgumentNullException.ThrowIfNull(actionCatalog);
        ArgumentNullException.ThrowIfNull(dispositionsCatalog);
        ArgumentNullException.ThrowIfNull(activity);

        // Derive valid dispositions from subject actions.
        var allActions = await actionCatalog.GetAllAsync();
        var subjectDispositionIds = allActions
            .Where(a => string.Equals(a.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.DispositionId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrEmpty(activity.DispositionId))
        {
            subjectDispositionIds.Add(activity.DispositionId);
        }

        var dispositions = await dispositionsCatalog.GetAsync(subjectDispositionIds);

        return dispositions.FirstOrDefault(d => d.ItemId == dispositionId);
    }
}
