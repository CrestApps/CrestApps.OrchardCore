using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tabular;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Writes the pay inputs the way the export pipeline does (rows grouped in the order first seen by timesheet, pay code
/// and the chosen columns, hours and amounts summed and rounded, the columns in order) with the old file's number
/// formats, so a test compares the values with the file the time clock's export wrote.
/// </summary>
internal static class PayInputCsv
{
    public static async Task<IReadOnlyList<PayInputRow>> ReadAsync(IServiceProvider services, PayInputQuery query)
    {
        var codes = (await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var schedules = (await services.GetRequiredService<IPayScheduleManager>().GetAllAsync()).ToDictionary(schedule => schedule.ItemId, StringComparer.Ordinal);

        return (await services.GetRequiredService<IPayInputService>().GetInputsAsync(query))
            .Select(input => new PayInputRow(input, codes.GetValueOrDefault(input.PayCodeId), schedules.GetValueOrDefault(input.PayScheduleId)))
            .ToList();
    }

    public static string Write(IReadOnlyList<PayInputRow> rows, IReadOnlyList<PayrollCsvColumn> columns = null)
    {
        columns = columns is { Count: > 0 } ? columns : PayrollCsvSettings.DefaultColumns;

        var fields = columns.Select(column => PayrollCsvPipeline.FieldOf(column.Field)).ToList();
        var dimensions = fields.Where(field => field is not ("Hours" or "Amount")).Distinct().ToList();
        var groups = new List<(string Key, List<PayInputRow> Rows)>();

        foreach (var row in rows)
        {
            var key = string.Join('|', new[] { row.Input.StaffMemberId, row.Input.PeriodStart.ToString("O"), row.Input.PayCodeId }.Concat(dimensions.Select(field => Value(row, field))));
            var group = groups.FirstOrDefault(item => item.Key == key);

            if (group.Rows is null)
            {
                group = (key, []);
                groups.Add(group);
            }

            group.Rows.Add(row);
        }

        // The pipeline sorts the groups as the old file did: by person, period, day when there is a day column, code.
        groups = [.. groups
            .OrderBy(group => group.Rows[0].Input.StaffMemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Rows[0].Input.StaffMemberId, StringComparer.Ordinal)
            .ThenBy(group => group.Rows[0].Input.PeriodStart)
            .ThenBy(group => dimensions.Contains("Date") ? group.Rows[0].Input.Date : null)
            .ThenBy(group => group.Rows[0].Code?.PayrollCode, StringComparer.OrdinalIgnoreCase)];

        var builder = new StringBuilder();

        builder.AppendJoin(',', columns.Select(column => CsvWriter.Escape(column.Header))).Append("\r\n");

        foreach (var (_, members) in groups)
        {
            var first = members[0];
            var hours = members.Sum(row => row.Input.Hours ?? 0);
            var amounts = members.Select(row => row.Amount).ToList();
            decimal? amount = amounts.Any(value => value is null) ? null : Math.Round(amounts.Sum(value => value.Value), 2, MidpointRounding.AwayFromZero);

            builder.AppendJoin(',', fields.Select(field => field switch
            {
                "Hours" => Math.Round(hours, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture),
                "Amount" => amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                "Rate" => first.Input.Rate?.ToString("0.00##", CultureInfo.InvariantCulture) ?? string.Empty,
                _ => CsvWriter.Escape(Value(first, field)),
            })).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string Value(PayInputRow row, string field)
        => field switch
        {
            "PayrollId" => row.Input.PayrollId,
            "EmployeeNumber" => row.Input.EmployeeNumber,
            "StaffMemberName" => row.Input.StaffMemberName,
            "PayCode" => row.Code?.PayrollCode,
            "Date" => row.Input.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Rate" => row.Input.Rate?.ToString(CultureInfo.InvariantCulture),
            "PeriodStart" => row.Input.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "PeriodEnd" => row.Input.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "ActivityCode" => row.Input.Allocation?.ActivityCode,
            "Job" => row.Input.Allocation?.Job,
            _ => null,
        };
}
