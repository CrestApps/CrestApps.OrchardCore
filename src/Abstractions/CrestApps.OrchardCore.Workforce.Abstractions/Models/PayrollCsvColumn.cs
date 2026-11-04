namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One column of the payroll CSV file.
/// </summary>
public sealed class PayrollCsvColumn
{
    /// <summary>
    /// Gets or sets the heading.
    /// </summary>
    public string Header { get; set; }

    /// <summary>
    /// Gets or sets what the column holds.
    /// </summary>
    public PayrollCsvField Field { get; set; }
}
