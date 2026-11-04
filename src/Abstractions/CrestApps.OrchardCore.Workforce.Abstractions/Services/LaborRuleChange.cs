using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A change to a published shift being checked, for the notice rules.
/// </summary>
public sealed class LaborRuleChange
{
    /// <summary>
    /// Gets or sets what changes.
    /// </summary>
    public ScheduleChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets when the shift started before.
    /// </summary>
    public DateTime? BeforeStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it starts after.
    /// </summary>
    public DateTime? AfterStartUtc { get; set; }

    /// <summary>
    /// Gets or sets who asked for it.
    /// </summary>
    public ScheduleChangeInitiator InitiatedBy { get; set; } = ScheduleChangeInitiator.Employer;
}
