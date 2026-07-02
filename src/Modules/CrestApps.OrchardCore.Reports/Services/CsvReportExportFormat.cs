using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Serializes a report document to CSV. Every section is written in order: metrics as label/value
/// pairs, tables as a header row plus data rows, and bars as label/value pairs.
/// </summary>
public sealed class CsvReportExportFormat : IReportExportFormat
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReportExportFormat"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CsvReportExportFormat(IStringLocalizer<CsvReportExportFormat> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.CsvExportFormat;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["CSV"];

    /// <inheritdoc/>
    public string ContentType => "text/csv";

    /// <inheritdoc/>
    public string FileExtension => "csv";

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

            if (!string.IsNullOrEmpty(section.Title))
            {
                AppendRow(builder, section.Title);
            }

            switch (section.Kind)
            {
                case ReportSectionKind.Metrics:
                    AppendRow(builder, "Metric", "Value");

                    foreach (var metric in section.Metrics)
                    {
                        AppendRow(builder, metric.Label, metric.Value);
                    }

                    break;
                case ReportSectionKind.Table:
                    AppendRow(builder, [.. section.Columns.Select(column => column.Label)]);

                    foreach (var row in section.Rows)
                    {
                        AppendRow(builder, [.. row.Cells]);
                    }

                    break;
                case ReportSectionKind.Bars:
                    AppendRow(builder, "Label", "Value");

                    foreach (var bar in section.Bars)
                    {
                        AppendRow(builder, bar.Label, bar.Value);
                    }

                    break;
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(values[i]));
        }

        builder.Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
