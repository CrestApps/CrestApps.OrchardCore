using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Runs payroll exports of locked timesheets and keeps their files.
/// </summary>
public interface IPayrollExportService
{
    /// <summary>
    /// Exports a pay period's locked timesheets that the provider hasn't exported yet, or all of them again.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="payPeriodCalendarId">The pay periods.</param>
    /// <param name="periodStart">The period's first day.</param>
    /// <param name="includeExported">Whether to export timesheets the provider already exported.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batch, or the reasons there is none.</returns>
    Task<PlanningResult<PayrollExportBatch>> RunAsync(string providerName, string payPeriodCalendarId, DateOnly periodStart, bool includeExported, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the exports, newest first.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<PayrollExportBatch> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an export.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batch, or <see langword="null"/>.</returns>
    Task<PayrollExportBatch> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the payroll CSV columns.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The settings.</returns>
    Task<PayrollCsvSettings> GetCsvSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the payroll CSV columns.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SaveCsvSettingsAsync(PayrollCsvSettings settings, CancellationToken cancellationToken = default);
}
