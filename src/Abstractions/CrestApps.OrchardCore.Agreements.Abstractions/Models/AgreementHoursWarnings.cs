namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// The shares of an agreement's hours observers were told about in a period.
/// </summary>
public sealed class AgreementHoursWarnings
{
    /// <summary>
    /// Gets or sets the first day of the period they were told in.
    /// </summary>
    public DateOnly? PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the shares, in percent, already told.
    /// </summary>
    public List<int> Percents { get; set; } = [];
}
