using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Opens an item a number of days after the enrollment was created.
/// </summary>
public sealed class DaysAfterEnrollmentReleaseRule : ICourseItemReleaseRule
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.DaysAfterEnrollment;

    /// <inheritdoc />
    public Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Settings?.Get("Days");

        if (value is null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days < 0 || context.Enrollment is null)
        {
            return Task.FromResult(ReleaseDecision.Locked("This item is not available yet."));
        }

        var opensUtc = DateTime.SpecifyKind(context.Enrollment.CreatedUtc, DateTimeKind.Utc).AddDays(days);

        return Task.FromResult(context.UtcNow >= opensUtc
            ? ReleaseDecision.Released
            : ReleaseDecision.Locked("This item opens later.", opensUtc));
    }
}
