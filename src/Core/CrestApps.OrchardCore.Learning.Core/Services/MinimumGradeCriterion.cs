using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when the course grade reaches a percentage.
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

        return Task.FromResult(context.Grade is { } grade && grade >= minimum);
    }
}
