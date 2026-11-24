using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when the learner spent a minimum total time on the course items.
/// </summary>
public sealed class MinimumTimeCriterion : ICourseCompletionCriterion
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.MinimumTime;

    /// <inheritdoc />
    public Task<bool> IsMetAsync(CompletionCriterionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Settings?.Get("Minutes");

        if (value is null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            return Task.FromResult(false);
        }

        var seconds = context.Progress?.Items.Sum(item => item.SecondsSpent) ?? 0;

        return Task.FromResult(seconds >= minutes * 60L);
    }
}
