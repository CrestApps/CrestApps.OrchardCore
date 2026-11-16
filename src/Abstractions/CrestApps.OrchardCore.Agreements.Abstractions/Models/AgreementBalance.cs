namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// How an agreement's hours stand in a period.
/// </summary>
public sealed class AgreementBalance
{
    /// <summary>
    /// Gets or sets the first day of the period: the agreement's start for a block, the month's first day for an
    /// allowance.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period, or <see langword="null"/> when a block runs until the agreement ends
    /// and it has no end.
    /// </summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the minutes the period covers.
    /// </summary>
    public double CoveredMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes used in the period, after the agreement's rounding.
    /// </summary>
    public double UsedMinutes { get; set; }

    /// <summary>
    /// Gets the minutes left, never below none.
    /// </summary>
    public double LeftMinutes => Math.Max(0, CoveredMinutes - UsedMinutes);

    /// <summary>
    /// Gets the minutes used past what the period covers, which are billable.
    /// </summary>
    public double OverageMinutes => Math.Max(0, UsedMinutes - CoveredMinutes);

    /// <summary>
    /// Gets the share of the covered minutes used, from 0; past 1 is overage.
    /// </summary>
    public double UsedShare => CoveredMinutes <= 0 ? 0 : UsedMinutes / CoveredMinutes;

    /// <summary>
    /// Gets or sets the entries counted, with their rounded minutes, earliest first.
    /// </summary>
    public IReadOnlyList<(AgreementUsage Usage, double RoundedMinutes)> Entries { get; set; } = [];
}
