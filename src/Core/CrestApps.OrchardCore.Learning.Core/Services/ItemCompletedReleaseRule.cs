using System.Globalization;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Opens an item once another course item is completed, optionally with a minimum score.
/// </summary>
public sealed class ItemCompletedReleaseRule : ICourseItemReleaseRule
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.ItemCompleted;

    /// <inheritdoc />
    public Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var itemId = context.Settings?.Get("ItemId");

        if (itemId is null)
        {
            return Task.FromResult(ReleaseDecision.Locked("This item is not available yet."));
        }

        var progress = context.Progress?.Find(itemId);

        if (progress is null || progress.State != CourseItemState.Completed)
        {
            return Task.FromResult(ReleaseDecision.Locked("Complete the earlier item first."));
        }

        var minimum = context.Settings.Get("MinimumScore");

        if (minimum is not null)
        {
            if (!decimal.TryParse(minimum, NumberStyles.Number, CultureInfo.InvariantCulture, out var minimumScore) || progress.Score is null || progress.Score < minimumScore)
            {
                return Task.FromResult(ReleaseDecision.Locked("Reach the required score on the earlier item first."));
            }
        }

        return Task.FromResult(ReleaseDecision.Released);
    }
}
