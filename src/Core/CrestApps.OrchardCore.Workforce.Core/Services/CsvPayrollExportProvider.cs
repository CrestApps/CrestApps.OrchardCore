using System.Globalization;
using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.Tabular;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The built-in <see cref="IPayrollExportProvider"/>: one CSV row per worker and pay code, or per day too when a
/// column asks for the day, with the columns set under Payroll Export.
/// </summary>
internal sealed class CsvPayrollExportProvider : IPayrollExportProvider
{
    public const string ProviderName = "Csv";

    private readonly PayrollCsvSettingsStore _settings;

    internal readonly IStringLocalizer S;

    public CsvPayrollExportProvider(PayrollCsvSettingsStore settings, IStringLocalizer<CsvPayrollExportProvider> stringLocalizer)
    {
        _settings = settings;
        S = stringLocalizer;
    }

    public string Name => ProviderName;

    public string DisplayName => S["CSV file"];

    public async Task<PayrollExportFile> ExportAsync(PayrollExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = await _settings.GetAsync(cancellationToken);
        var columns = settings.Columns.Count > 0 ? settings.Columns.ToArray() : [.. PayrollCsvSettings.DefaultColumns];
        var byDay = columns.Any(column => column.Field == PayrollCsvField.Date);
        var byActivity = columns.Any(column => column.Field == PayrollCsvField.ActivityCode);
        var byJob = columns.Any(column => column.Field == PayrollCsvField.Job);
        var builder = new StringBuilder();
        var rows = 0;

        builder.AppendJoin(',', columns.Select(column => CsvWriter.Escape(column.Header))).Append("\r\n");

        foreach (var timesheet in context.Timesheets)
        {
            var worker = context.Workers.GetValueOrDefault(timesheet.WorkerId);
            var rates = worker is not null && worker.TryGet<WorkerPayRates>(out var stored) ? stored : new WorkerPayRates();
            var groups = timesheet.Lines
                .Where(line => !string.IsNullOrEmpty(line.PayCodeId) && line.Hours != 0)
                .GroupBy(line => (
                    line.PayCodeId,
                    Date: byDay ? line.Date : (DateOnly?)null,
                    Activity: byActivity ? line.ActivityCodeId : null,
                    Job: byJob ? line.Job : null))
                .OrderBy(group => group.Key.Date)
                .ThenBy(group => context.PayCodes.GetValueOrDefault(group.Key.PayCodeId)?.Code, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var code = context.PayCodes.GetValueOrDefault(group.Key.PayCodeId);
                var hours = group.Sum(line => line.Hours);
                var rate = rates.RateOn(group.Key.Date ?? timesheet.PeriodEnd, group.Key.Activity);
                var amount = Amount(code, hours, rate);
                var values = columns.Select(column => column.Field switch
                {
                    PayrollCsvField.WorkerExternalId => CsvWriter.Escape(worker?.ExternalId ?? worker?.EmployeeNumber),
                    PayrollCsvField.EmployeeNumber => CsvWriter.Escape(worker?.EmployeeNumber),
                    PayrollCsvField.WorkerName => CsvWriter.Escape(worker?.DisplayName),
                    PayrollCsvField.PayCode => CsvWriter.Escape(code?.PayrollCode),
                    PayrollCsvField.Date => group.Key.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    PayrollCsvField.Hours => hours.ToString("0.00", CultureInfo.InvariantCulture),
                    PayrollCsvField.Rate => rate?.ToString("0.00##", CultureInfo.InvariantCulture) ?? string.Empty,
                    PayrollCsvField.Amount => amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                    PayrollCsvField.PeriodStart => timesheet.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    PayrollCsvField.PeriodEnd => timesheet.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    PayrollCsvField.ActivityCode => CsvWriter.Escape(group.Key.Activity),
                    PayrollCsvField.Job => CsvWriter.Escape(group.Key.Job),
                    _ => string.Empty,
                });

                builder.AppendJoin(',', values).Append("\r\n");
                rows++;
            }
        }

        return new PayrollExportFile
        {
            FileName = $"payroll-{context.PeriodStart:yyyy-MM-dd}-to-{context.PeriodEnd:yyyy-MM-dd}.csv",
            ContentType = "text/csv",
            Content = builder.ToString(),
            RowCount = rows,
        };
    }

    /// <summary>
    /// Returns what the hours pay: a premium an hour when the code has one, otherwise the rate times the multiplier.
    /// </summary>
    internal static decimal? Amount(PayCode code, decimal hours, decimal? rate)
    {
        if (code is null || code.Kind == PayCodeKind.Unpaid)
        {
            return code is null ? null : 0;
        }

        if (code.PremiumPerHour is > 0)
        {
            return Math.Round(hours * code.PremiumPerHour.Value, 2);
        }

        return rate.HasValue ? Math.Round(hours * rate.Value * code.Multiplier, 2) : null;
    }
}
