using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tabular;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Serializes a report document to CSV with <see cref="CsvWriter"/>, which guards against formula injection. Every
/// section is written in order: metrics as label/value pairs, tables as a header row plus data rows, bars as label/value
/// pairs, and charts as label/series tables.
/// </summary>
public sealed class CsvReportExportFormat : IReportExportFormat
{
    /// <inheritdoc/>
    public static string Name => ReportsConstants.CsvExportFormat;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CsvReportExportFormat>("CSV");

    /// <inheritdoc/>
    public static string Extension => ".csv";

    /// <inheritdoc/>
    public static string ContentType => "text/csv";

    /// <inheritdoc/>
    public byte[] Serialize(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        var first = true;

        foreach (var section in document.Sections)
        {
            if (!first)
            {
                builder.Append("\r\n");
            }

            first = false;

            switch (section.Kind)
            {
                case ReportSectionKind.Metrics:
                    CsvWriter.AppendRow(builder, "Metric", "Value");

                    foreach (var metric in section.Metrics)
                    {
                        CsvWriter.AppendRow(builder, metric.Label, metric.Value);
                    }

                    break;
                case ReportSectionKind.Table:
                    // The rows that group the columns: each label in the first cell of the columns it spans.
                    foreach (var headerRow in section.HeaderRows ?? [])
                    {
                        CsvWriter.AppendRow(builder, [.. (headerRow?.Cells ?? []).SelectMany(cell => Enumerable.Range(0, Math.Max(1, cell.Span)).Select(position => position == 0 ? cell.Label : string.Empty))]);
                    }

                    CsvWriter.AppendRow(builder, [.. section.Columns.Select(column => column.Label)]);

                    foreach (var row in section.Rows)
                    {
                        CsvWriter.AppendRow(builder, [.. row.Cells]);
                    }

                    break;
                case ReportSectionKind.Bars:
                    CsvWriter.AppendRow(builder, "Label", "Value");

                    foreach (var bar in section.Bars)
                    {
                        CsvWriter.AppendRow(builder, bar.Label, bar.Value);
                    }

                    break;
                case ReportSectionKind.Chart:
                    AppendChart(builder, section.Chart);
                    break;
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static void AppendChart(StringBuilder builder, ReportChart chart)
    {
        if (chart is null)
        {
            return;
        }

        CsvWriter.AppendRow(builder, ["Label", .. chart.Datasets.Select(dataset => dataset.Label)]);

        for (var labelIndex = 0; labelIndex < chart.Labels.Count; labelIndex++)
        {
            var values = new string[chart.Datasets.Count + 1];
            values[0] = chart.Labels[labelIndex];

            for (var datasetIndex = 0; datasetIndex < chart.Datasets.Count; datasetIndex++)
            {
                var dataset = chart.Datasets[datasetIndex];
                values[datasetIndex + 1] = labelIndex < dataset.Values.Count
                    ? dataset.Values[labelIndex].ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }

            CsvWriter.AppendRow(builder, values);
        }
    }
}
