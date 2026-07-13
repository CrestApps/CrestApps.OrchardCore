using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.OpenXml.Services;

/// <summary>
/// Serializes a report document to an Excel workbook where each report section is written to its own
/// worksheet using the Open XML SDK.
/// </summary>
public sealed class ExcelReportExportFormat : IReportExportFormat
{
    private const string DefaultSheetName = "Report";
    private static readonly char[] _invalidSheetNameCharacters = ['\\', '/', '*', '[', ']', ':', '?'];

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelReportExportFormat"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExcelReportExportFormat(IStringLocalizer<ExcelReportExportFormat> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.XlsxExportFormat;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Excel (.xlsx)"];

    /// <inheritdoc/>
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc/>
    public string FileExtension => "xlsx";

    /// <inheritdoc/>
    public byte[] Serialize(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var stream = new MemoryStream();

        using (var spreadsheetDocument = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = spreadsheetDocument.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            uint sheetId = 1;

            if (document.Sections.Count == 0)
            {
                AddWorksheet(workbookPart, sheets, usedSheetNames, DefaultSheetName, sheetId++, 0);
            }
            else
            {
                for (var i = 0; i < document.Sections.Count; i++)
                {
                    var section = document.Sections[i];
                    var sheetData = AddWorksheet(
                        workbookPart,
                        sheets,
                        usedSheetNames,
                        section.Title,
                        sheetId,
                        i + 1);

                    WriteSection(sheetData, section);
                    sheetId++;
                }
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static SheetData AddWorksheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        ISet<string> usedSheetNames,
        string title,
        uint sheetId,
        int sectionIndex)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = CreateUniqueSheetName(title, sectionIndex, usedSheetNames),
        });

        return sheetData;
    }

    private static string CreateUniqueSheetName(string title, int index, ISet<string> usedSheetNames)
    {
        var baseName = SanitizeSheetName(string.IsNullOrWhiteSpace(title) ? $"{DefaultSheetName} {index}" : title);
        var candidate = baseName;
        var suffix = 2;

        while (!usedSheetNames.Add(candidate))
        {
            var suffixText = $" ({suffix})";
            var maxLength = 31 - suffixText.Length;
            var trimmedBaseName = baseName.Length > maxLength ? baseName.Substring(0, maxLength) : baseName;
            candidate = trimmedBaseName + suffixText;
            suffix++;
        }

        return candidate;
    }

    private static string SanitizeSheetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultSheetName;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
        {
            builder.Append(_invalidSheetNameCharacters.Contains(character) ? ' ' : character);
        }

        var sanitized = builder.ToString().Trim().Trim('\'');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = DefaultSheetName;
        }

        if (sanitized.Length > 31)
        {
            sanitized = sanitized.Substring(0, 31);
        }

        return sanitized;
    }

    private static void WriteSection(SheetData sheetData, ReportSection section)
    {
        if (!string.IsNullOrWhiteSpace(section.Description))
        {
            AppendTextRow(sheetData, section.Description);
            AppendEmptyRow(sheetData);
        }

        switch (section.Kind)
        {
            case ReportSectionKind.Metrics:
                WriteMetricsSection(sheetData, section);
                break;
            case ReportSectionKind.Table:
                WriteTableSection(sheetData, section);
                break;
            case ReportSectionKind.Bars:
                WriteBarsSection(sheetData, section);
                break;
            case ReportSectionKind.Chart:
                WriteChartSection(sheetData, section.Chart);
                break;
        }
    }

    private static void WriteMetricsSection(SheetData sheetData, ReportSection section)
    {
        var hasHints = section.Metrics.Any(metric => !string.IsNullOrWhiteSpace(metric.Hint));

        AppendTextRow(sheetData, hasHints ? ["Metric", "Value", "Hint"] : ["Metric", "Value"]);

        foreach (var metric in section.Metrics)
        {
            AppendTextRow(sheetData, hasHints ? [metric.Label, metric.Value, metric.Hint] : [metric.Label, metric.Value]);
        }
    }

    private static void WriteTableSection(SheetData sheetData, ReportSection section)
    {
        if (section.Columns.Count > 0)
        {
            AppendTextRow(sheetData, [.. section.Columns.Select(column => column.Label)]);
        }

        foreach (var row in section.Rows)
        {
            AppendTextRow(sheetData, [.. row.Cells]);
        }
    }

    private static void WriteBarsSection(SheetData sheetData, ReportSection section)
    {
        AppendTextRow(sheetData, "Label", "Value", "Ratio");

        foreach (var bar in section.Bars)
        {
            AppendTextRow(sheetData, bar.Label, bar.Value, bar.Ratio.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void WriteChartSection(SheetData sheetData, ReportChart chart)
    {
        if (chart is null)
        {
            return;
        }

        AppendTextRow(sheetData, ["Label", .. chart.Datasets.Select(dataset => dataset.Label)]);

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

            AppendTextRow(sheetData, values);
        }
    }

    private static void AppendEmptyRow(SheetData sheetData)
    {
        sheetData.Append(new Row());
    }

    private static void AppendTextRow(SheetData sheetData, params string[] values)
    {
        var row = new Row();

        foreach (var value in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty)),
            });
        }

        sheetData.Append(row);
    }
}
