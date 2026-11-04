namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The payroll CSV file's columns.
/// </summary>
public sealed class PayrollCsvSettings
{
    /// <summary>
    /// Gets or sets the columns; empty for the built-in ones.
    /// </summary>
    public IList<PayrollCsvColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets the built-in columns.
    /// </summary>
    public static IReadOnlyList<PayrollCsvColumn> DefaultColumns { get; } =
    [
        new() { Header = "EmployeeId", Field = PayrollCsvField.WorkerExternalId },
        new() { Header = "Name", Field = PayrollCsvField.WorkerName },
        new() { Header = "PeriodStart", Field = PayrollCsvField.PeriodStart },
        new() { Header = "PeriodEnd", Field = PayrollCsvField.PeriodEnd },
        new() { Header = "PayCode", Field = PayrollCsvField.PayCode },
        new() { Header = "Hours", Field = PayrollCsvField.Hours },
        new() { Header = "Rate", Field = PayrollCsvField.Rate },
        new() { Header = "Amount", Field = PayrollCsvField.Amount },
    ];
}
