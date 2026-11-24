using System.Globalization;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Opens an item from a date.
/// </summary>
public sealed class DateReleaseRule : ICourseItemReleaseRule
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.Date;

    /// <inheritdoc />
    public Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Settings?.Get("FromUtc");

        if (value is null || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fromUtc))
        {
            return Task.FromResult(ReleaseDecision.Locked(LearningRefusals.NotAvailableYet));
        }

        fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);

        return Task.FromResult(context.UtcNow >= fromUtc
            ? ReleaseDecision.Released
            : ReleaseDecision.Locked(LearningRefusals.OpensLater, fromUtc));
    }
}
