using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The time rule Workforce keeps on a pay code: the hours of a shift differential.
/// </summary>
/// <remarks>
/// Stored in the pay code's <c>Properties</c>, because the window is a time rule, not a pay rule.
/// </remarks>
public sealed class WorkforcePayCodeSettings
{
    /// <summary>
    /// Gets or sets when a shift differential's window starts, in the worker's time.
    /// </summary>
    public TimeOnly? WindowStart { get; set; }

    /// <summary>
    /// Gets or sets when a shift differential's window ends; an end before the start runs past midnight.
    /// </summary>
    public TimeOnly? WindowEnd { get; set; }

    /// <summary>
    /// Gets or sets the days the window starts on; none means every day.
    /// </summary>
    public IList<DayOfWeek> Weekdays { get; set; } = [];
}

/// <summary>
/// Reads Workforce's settings on a pay code.
/// </summary>
public static class WorkforcePayCodeExtensions
{
    private static readonly WorkforcePayCodeSettings _none = new();

    /// <summary>
    /// Gets the code's shift differential window, or an empty one.
    /// </summary>
    /// <param name="code">The pay code.</param>
    /// <returns>The settings; never <see langword="null"/>.</returns>
    public static WorkforcePayCodeSettings GetWorkforceSettings(this PayCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.TryGet<WorkforcePayCodeSettings>(out var settings) && settings is not null ? settings : _none;
    }
}
