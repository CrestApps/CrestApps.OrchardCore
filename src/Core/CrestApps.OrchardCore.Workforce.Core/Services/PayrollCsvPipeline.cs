using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Turns the columns of the time clock's old payroll CSV into a data pipeline over the Pay Inputs data set that writes
/// the same file: a row per timesheet and pay code (and per day, activity or job when such a column is chosen), the
/// hours and amounts summed and rounded, and the columns in their order under their headings. The pipeline marks the
/// pay inputs exported once a run succeeds.
/// </summary>
public static class PayrollCsvPipeline
{
    /// <summary>
    /// The id of the pipeline made from a site's saved columns or from a recipe's.
    /// </summary>
    public const string PipelineId = "workforcepayrollcsv0000001";

    private static readonly string[] _measures = ["Hours", "Amount"];

    /// <summary>
    /// Gets the Pay Inputs field each column reads.
    /// </summary>
    /// <param name="field">The column.</param>
    /// <returns>The field's name.</returns>
    public static string FieldOf(PayrollCsvField field)
        => field switch
        {
            PayrollCsvField.WorkerExternalId => "PayrollId",
            PayrollCsvField.EmployeeNumber => "EmployeeNumber",
            PayrollCsvField.WorkerName => "StaffMemberName",
            PayrollCsvField.PayCode => "PayCode",
            PayrollCsvField.Date => "Date",
            PayrollCsvField.Hours => "Hours",
            PayrollCsvField.Rate => "Rate",
            PayrollCsvField.Amount => "Amount",
            PayrollCsvField.PeriodStart => "PeriodStart",
            PayrollCsvField.PeriodEnd => "PeriodEnd",
            PayrollCsvField.ActivityCode => "ActivityCode",
            PayrollCsvField.Job => "Job",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

    /// <summary>
    /// Builds the entry of the DataPipelines recipe step that makes the pipeline, as a draft to publish.
    /// </summary>
    /// <param name="columns">The columns, or none for the usual ones.</param>
    /// <param name="name">The pipeline's name.</param>
    /// <returns>The pipeline entry.</returns>
    public static JsonObject Build(IEnumerable<PayrollCsvColumn> columns, string name = "Payroll CSV export")
    {
        var chosen = (columns ?? []).Where(column => column is not null && Enum.IsDefined(column.Field)).ToList();

        if (chosen.Count == 0)
        {
            chosen = [.. PayrollCsvSettings.DefaultColumns];
        }

        var fields = chosen.Select(column => FieldOf(column.Field)).ToList();
        var dimensions = fields.Where(field => !_measures.Contains(field)).Distinct(StringComparer.Ordinal).ToList();
        var measures = fields.Where(_measures.Contains).Distinct(StringComparer.Ordinal).ToList();

        // The old file always had a row per timesheet and pay code, whatever its columns. The name and the code depend
        // on those, so grouping by them too changes nothing but keeps them to sort by.
        var groupBy = new List<string> { "StaffMemberId", "StaffMemberName", "PeriodStart", "PayCodeId", "PayCode" };

        groupBy.AddRange(dimensions.Where(field => !groupBy.Contains(field)));

        var readFields = groupBy.Concat(measures).Concat(["SourceKind", "SourceId"]).Distinct(StringComparer.Ordinal).ToList();
        var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selected = new JsonArray();

        foreach (var column in chosen)
        {
            var header = string.IsNullOrWhiteSpace(column.Header) ? FieldOf(column.Field) : column.Header.Trim();
            var unique = header;

            for (var i = 2; !headers.Add(unique); i++)
            {
                unique = $"{header}{i}";
            }

            selected.Add(new JsonObject { ["Field"] = FieldOf(column.Field), ["Name"] = unique });
        }

        var steps = new JsonArray
        {
            Step("inputs", "DataSource", "Pay inputs", 120, new JsonObject
            {
                ["DataSourceStepSettings"] = new JsonObject
                {
                    ["Source"] = PayrollConstants.DataSourceName,
                    ["DataSet"] = PayrollConstants.PayInputsDataSetName,
                    ["Fields"] = ToArray(readFields),
                },
            }),
            Step("mark", PayrollConstants.MarkPayInputsExportedStepName, "Mark exported", 360, []),
            Step("group", "Aggregate", "A row per timesheet and pay code", 600, new JsonObject
            {
                ["AggregateStepSettings"] = new JsonObject
                {
                    ["GroupBy"] = ToArray(groupBy),
                    ["Measures"] = new JsonArray([.. measures.Select(measure => (JsonNode)new JsonObject { ["Name"] = measure, ["Function"] = "Sum", ["Field"] = measure })]),
                },
            }),
            Step("round", "CalculatedFields", "Round the sums", 840, new JsonObject
            {
                ["CalculatedFieldsStepSettings"] = new JsonObject
                {
                    ["Fields"] = new JsonArray([.. measures.Select(measure => (JsonNode)new JsonObject { ["Name"] = measure, ["Formula"] = $"ROUND([{measure}], 2)" })]),
                },
            }),
            Step("sort", "Sort", "By person, period, day and code", 1080, new JsonObject
            {
                ["SortStepSettings"] = new JsonObject
                {
                    ["Keys"] = new JsonArray([.. SortKeys(dimensions).Select(field => (JsonNode)new JsonObject { ["Field"] = field, ["Descending"] = false })]),
                },
            }),
            Step("columns", "SelectFields", "Columns", 1320, new JsonObject
            {
                ["SelectFieldsStepSettings"] = new JsonObject { ["Fields"] = selected },
            }),
            Step("file", "CreateFile", "Payroll CSV", 1560, new JsonObject
            {
                ["CreateFileStepSettings"] = new JsonObject
                {
                    ["Format"] = "csv",
                    ["FileName"] = "payroll-{Date:yyyyMMdd-HHmmss}",
                    ["UseDisplayNames"] = false,
                },
            }),
        };

        return new JsonObject
        {
            ["PipelineId"] = PipelineId,
            ["Name"] = name,
            ["Description"] = "The pay inputs not exported yet, such as locked timesheets, in the columns the time clock's payroll export used. They are marked exported once a run succeeds.",
            ["IsEnabled"] = true,
            ["Definition"] = new JsonObject
            {
                ["Steps"] = steps,
                ["Connections"] = new JsonArray(
                    Connection("inputs", "mark"),
                    Connection("mark", "group"),
                    Connection("group", "round"),
                    Connection("round", "sort"),
                    Connection("sort", "columns"),
                    Connection("columns", "file")),
            },
        };
    }

    /// <summary>
    /// Builds the whole DataPipelines recipe step.
    /// </summary>
    /// <param name="columns">The columns, or none for the usual ones.</param>
    /// <returns>The step.</returns>
    public static JsonObject BuildStep(IEnumerable<PayrollCsvColumn> columns)
        => new()
        {
            ["name"] = "DataPipelines",
            ["Pipelines"] = new JsonArray(Build(columns)),
        };

    /// <summary>
    /// Gets the order of the old file: by person, period, then day when it has a day column, then pay code.
    /// </summary>
    /// <param name="dimensions">The fields the columns read, other than hours and amounts.</param>
    /// <returns>The fields to sort by.</returns>
    public static IReadOnlyList<string> SortKeys(IEnumerable<string> dimensions)
    {
        var keys = new List<string> { "StaffMemberName", "StaffMemberId", "PeriodStart" };

        if (dimensions.Contains("Date"))
        {
            keys.Add("Date");
        }

        keys.Add("PayCode");

        return keys;
    }

    private static JsonObject Step(string id, string type, string title, int x, JsonObject properties)
        => new()
        {
            ["StepId"] = id,
            ["Type"] = type,
            ["Title"] = title,
            ["X"] = x,
            ["Y"] = 320,
            ["Properties"] = properties,
        };

    private static JsonObject Connection(string from, string to)
        => new()
        {
            ["SourceStepId"] = from,
            ["SourcePort"] = "Output",
            ["TargetStepId"] = to,
            ["TargetPort"] = "Input",
        };

    private static JsonArray ToArray(IEnumerable<string> values)
        => new([.. values.Select(value => (JsonNode)value)]);
}
