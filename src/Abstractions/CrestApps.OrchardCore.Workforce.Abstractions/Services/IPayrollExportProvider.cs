namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Writes locked timesheets for a payroll system. The built-in provider writes a CSV file.
/// </summary>
public interface IPayrollExportProvider
{
    /// <summary>
    /// Gets the provider's technical name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown in the admin.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Writes the timesheets.
    /// </summary>
    /// <param name="context">The timesheets and what they refer to.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The file.</returns>
    Task<PayrollExportFile> ExportAsync(PayrollExportContext context, CancellationToken cancellationToken = default);
}
