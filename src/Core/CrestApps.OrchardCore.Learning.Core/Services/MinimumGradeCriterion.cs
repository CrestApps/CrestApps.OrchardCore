using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when the course grade reaches a percentage. When nothing in the course gives the learner a grade (no item counts
/// toward it, or every one that does recorded the learner's completion only, as an anonymous quiz does), the criterion
/// has no grade to check and is met, so it never keeps the course from completing; the course editor warns about such a
/// course. Any other missing grade is not met (R9-M3).
/// </summary>
public sealed class MinimumGradeCriterion : ICourseCompletionCriterion
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.MinimumGrade;

    /// <inheritdoc />
    public Task<bool> IsMetAsync(CompletionCriterionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Settings?.Get("Grade");

        if (value is null || !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var minimum))
        {
            return Task.FromResult(false);
        }

        // An item without a score counts as 0, so a learner who simply hasn't been scored yet has a grade of 0, not null.
        return Task.FromResult(context.Grade is { } grade ? grade >= minimum : context.NoGradeSource);
    }
}
