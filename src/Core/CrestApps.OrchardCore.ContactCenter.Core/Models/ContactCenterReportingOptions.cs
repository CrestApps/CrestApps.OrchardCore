namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The bounds the interactive Contact Center reports are generated within. Reports materialize every
/// interaction and activity in the requested window, so an unbounded range would let a single admin request
/// load an arbitrarily large result set into memory. Silently trimming rows would corrupt the aggregate
/// totals, so the range is capped and an over-wide request is rejected instead.
/// </summary>
public sealed class ContactCenterReportingOptions
{
    /// <summary>
    /// Gets or sets the widest reporting window a single report request may span. A request whose
    /// <c>toUtc - fromUtc</c> exceeds this value is rejected before any data is queried. The default of
    /// 400 days comfortably covers the built-in day, week, month, quarter, and year presets while still
    /// bounding an ad-hoc request.
    /// </summary>
    public TimeSpan MaximumReportRange { get; set; } = TimeSpan.FromDays(400);
}
