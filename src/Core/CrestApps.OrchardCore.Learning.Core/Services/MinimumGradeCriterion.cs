using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when the course grade reaches a percentage. A course grade is <see langword="null"/> only when nothing in the course
/// counts toward it for the learner (no item counts toward the grade, or every one that does recorded the learner's
/// completion only, as an anonymous quiz does): the criterion then has no grade to check and is met, so it never keeps the
/// course from completing. The course editor warns when the course has nothing that gives a grade.
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

        // No grade means nothing in the course gives one (see CourseGradeCalculator): an item without a score counts as 0,
        // so a learner who simply hasn't been scored yet has a grade of 0, not null.
        return Task.FromResult(context.Grade is not { } grade || grade >= minimum);
    }
}
