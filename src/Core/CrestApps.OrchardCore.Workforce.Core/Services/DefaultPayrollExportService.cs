using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IPayrollExportService"/>. A run takes the period's export lock, so two people exporting
/// at once can't both export the same timesheets.
/// </summary>
internal sealed class DefaultPayrollExportService : IPayrollExportService
{
    private readonly IEnumerable<IPayrollExportProvider> _providers;
    private readonly PayrollExportBatchStore _batchStore;
    private readonly TimesheetStore _timesheetStore;
    private readonly PayPeriodCalendarStore _calendarStore;
    private readonly PayCodeStore _payCodeStore;
    private readonly WorkerStore _workerStore;
    private readonly PayrollCsvSettingsStore _csvSettings;
    private readonly TimeOffLocks _locks;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultPayrollExportService(
        IEnumerable<IPayrollExportProvider> providers,
        PayrollExportBatchStore batchStore,
        TimesheetStore timesheetStore,
        PayPeriodCalendarStore calendarStore,
        PayCodeStore payCodeStore,
        WorkerStore workerStore,
        PayrollCsvSettingsStore csvSettings,
        TimeOffLocks locks,
        IServiceProvider serviceProvider,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultPayrollExportService> stringLocalizer)
    {
        _providers = providers;
        _batchStore = batchStore;
        _timesheetStore = timesheetStore;
        _calendarStore = calendarStore;
        _payCodeStore = payCodeStore;
        _workerStore = workerStore;
        _csvSettings = csvSettings;
        _locks = locks;
        _serviceProvider = serviceProvider;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<PlanningResult<PayrollExportBatch>> RunAsync(string providerName, string payPeriodCalendarId, DateOnly periodStart, bool includeExported, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<PayrollExportBatch>();
        var provider = _providers.FirstOrDefault(provider => string.Equals(provider.Name, providerName, StringComparison.OrdinalIgnoreCase));
        var calendar = string.IsNullOrEmpty(payPeriodCalendarId) ? null : await _calendarStore.FindByIdAsync(payPeriodCalendarId, cancellationToken);

        if (provider is null)
        {
            result.Fail(S["Choose how to export."]);
        }

        if (calendar is null)
        {
            result.Fail(S["Choose the pay periods."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var (start, end) = calendar.PeriodOn(periodStart);
        var lockers = await _locks.LockAsync([TimeOffLocks.PayrollExport(calendar.ItemId, start)]);

        if (lockers is null)
        {
            result.Fail(S["This period is being exported by someone else. Try again in a moment."]);

            return result;
        }

        try
        {
            var locked = await _timesheetStore.ListForPeriodAsync(calendar.ItemId, start, TimesheetStatus.Locked, cancellationToken);

            if (locked.Count == 0)
            {
                result.Fail(S["No timesheets of {0} to {1} are locked yet. Approve them, then lock the period.", start.ToString("d", CultureInfo.CurrentCulture), end.ToString("d", CultureInfo.CurrentCulture)]);

                return result;
            }

            var exportedBefore = await _batchStore.ListIdsAsync(provider.Name, calendar.ItemId, start, cancellationToken);
            var timesheets = includeExported
                ? locked
                : locked.Where(timesheet => !timesheet.ExportBatchIds.Any(exportedBefore.Contains)).ToArray();

            if (timesheets.Count == 0)
            {
                result.Fail(S["Every locked timesheet of this period was exported already. Choose to export them again to include them."]);

                return result;
            }

            var workers = (await _workerStore.GetAsync(timesheets.Select(timesheet => timesheet.WorkerId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken))
                .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
            var payCodes = (await _payCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
            var file = await provider.ExportAsync(new PayrollExportContext
            {
                Calendar = calendar,
                PeriodStart = start,
                PeriodEnd = end,
                Timesheets = [.. timesheets.OrderBy(timesheet => workers.GetValueOrDefault(timesheet.WorkerId)?.DisplayName, StringComparer.CurrentCultureIgnoreCase)],
                Workers = workers,
                PayCodes = payCodes,
            }, cancellationToken);

            var batch = new PayrollExportBatch
            {
                ItemId = UniqueId.GenerateId(),
                ProviderName = provider.Name,
                PayPeriodCalendarId = calendar.ItemId,
                PeriodStart = start,
                PeriodEnd = end,
                TimesheetIds = [.. timesheets.Select(timesheet => timesheet.ItemId)],
                RowCount = file.RowCount,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = file.Content,
                CreatedBy = actor,
                CreatedUtc = _clock.UtcNow,
            };

            await _batchStore.CreateAsync(batch, cancellationToken);

            foreach (var timesheet in timesheets)
            {
                timesheet.ExportBatchIds.Add(batch.ItemId);
                await _timesheetStore.UpdateAsync(timesheet, cancellationToken);
            }

            await MarkPredictabilityPayExportedAsync(timesheets, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            result.Value = batch;

            return result;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    public Task<(int Count, IReadOnlyList<PayrollExportBatch> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return _batchStore.PageAsync(skip, take, cancellationToken);
    }

    public async Task<PayrollExportBatch> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _batchStore.FindByIdAsync(id, cancellationToken);
    }

    public Task<PayrollCsvSettings> GetCsvSettingsAsync(CancellationToken cancellationToken = default)
    {
        return _csvSettings.GetAsync(cancellationToken);
    }

    public Task SaveCsvSettingsAsync(PayrollCsvSettings settings, CancellationToken cancellationToken = default)
    {
        return _csvSettings.SaveAsync(settings, cancellationToken);
    }

    /// <summary>
    /// Marks the predictability pay the exported timesheets pay as exported.
    /// </summary>
    private async Task MarkPredictabilityPayExportedAsync(IEnumerable<Timesheet> timesheets, CancellationToken cancellationToken)
    {
        var store = _serviceProvider.GetService<PredictabilityPayStore>();

        if (store is null)
        {
            return;
        }

        var ids = timesheets
            .SelectMany(timesheet => timesheet.Lines)
            .Where(line => line.Source == TimesheetLineSource.PredictabilityPay)
            .SelectMany(line => line.SourceIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var entry in await store.GetAsync(ids, cancellationToken))
        {
            if (entry.Status == PredictabilityPayStatus.Approved)
            {
                entry.Status = PredictabilityPayStatus.Exported;
                await store.UpdateAsync(entry, cancellationToken);
            }
        }
    }
}
