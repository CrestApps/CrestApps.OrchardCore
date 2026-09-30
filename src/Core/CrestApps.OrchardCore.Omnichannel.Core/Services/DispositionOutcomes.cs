using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finds the disposition that stands for an outcome, for a call that ended in a way nobody chose.
/// </summary>
public static class DispositionOutcomes
{
    /// <summary>
    /// The disposition with this outcome that the subject's own flow is wired to, so the subject's actions for it run;
    /// or, when <paramref name="includeUnwired"/> is set and the flow wires none, the first disposition with the outcome.
    /// </summary>
    /// <param name="dispositions">The dispositions to choose from.</param>
    /// <param name="subjectActions">Every configured subject action.</param>
    /// <param name="subjectContentType">The subject content type of the activity.</param>
    /// <param name="outcome">The outcome to find.</param>
    /// <param name="includeUnwired">Whether a disposition the subject's flow does not use may be returned.</param>
    /// <returns>The disposition, or <see langword="null"/> when none has the outcome.</returns>
    public static OmnichannelDisposition Find(
        IEnumerable<OmnichannelDisposition> dispositions,
        IEnumerable<SubjectAction> subjectActions,
        string subjectContentType,
        DispositionOutcome outcome,
        bool includeUnwired)
    {
        if (dispositions is null || outcome == DispositionOutcome.None)
        {
            return null;
        }

        var candidates = dispositions
            .Where(disposition => disposition is not null && disposition.Outcome == outcome)
            .OrderBy(disposition => disposition.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(subjectContentType))
        {
            var wiredIds = (subjectActions ?? [])
                .Where(action => action is not null &&
                    string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(action.DispositionId))
                .Select(action => action.DispositionId)
                .ToHashSet(StringComparer.Ordinal);

            var wired = candidates.FirstOrDefault(disposition => wiredIds.Contains(disposition.ItemId));

            if (wired is not null)
            {
                return wired;
            }
        }

        return includeUnwired ? candidates[0] : null;
    }
}
