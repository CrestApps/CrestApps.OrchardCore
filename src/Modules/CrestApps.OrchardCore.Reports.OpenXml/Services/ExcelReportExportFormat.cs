using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.OpenXml.Services;

/// <summary>
/// Serializes a report document to an Excel workbook using the Open XML SDK. Each container is written to its own
/// worksheet named after it, with its sections one under the other, and each section outside a container to a worksheet
/// of its own.
/// </summary>
public sealed class ExcelReportExportFormat : IReportExportFormat
{
    private const string DefaultSheetName = "Report";
    private static readonly char[] _invalidSheetNameCharacters = ['\\', '/', '*', '[', ']', ':', '?'];

    /// <inheritdoc/>
    public static string Name => ReportsConstants.XlsxExportFormat;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ExcelReportExportFormat>("Excel (.xlsx)");

    /// <inheritdoc/>
    public static string Extension => ".xlsx";

    /// <inheritdoc/>
    public static string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

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
            var styleRegistry = new ExcelStyleRegistry();
            uint sheetId = 1;

            var groups = document.GetSectionGroups();

            if (groups.Count == 0)
            {
                AddWorksheet(workbookPart, sheets, usedSheetNames, DefaultSheetName, sheetId++, 0);
            }
            else
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    var sheetData = AddWorksheet(
                        workbookPart,
                        sheets,
                        usedSheetNames,
                        GetSheetTitle(document, groups, i),
                        sheetId,
                        i + 1);

                    // A container's sections share its worksheet, each under its title; a section on a worksheet of its
                    // own is named by the worksheet.
                    for (var j = 0; j < group.Sections.Count; j++)
                    {
                        var section = group.Sections[j];

                        if (group.Container is not null)
                        {
                            if (j > 0)
                            {
                                AppendEmptyRow(sheetData);
                            }

                            if (!string.IsNullOrWhiteSpace(section.Title))
                            {
                                sheetData.Append(new Row(CreateCell(section.Title, styleRegistry.GetCellFormatIndex(ReportStyle.Create(bold: true)))));
                            }
                        }

                        WriteSection(sheetData, document, section, styleRegistry);
                    }

                    sheetId++;
                }
            }

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = styleRegistry.Build();
            stylesPart.Stylesheet.Save();

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    // A container's worksheet is named after it and a section's after the section; a report with one section on its own
    // names it after the report, and worksheets without a title are numbered after the report.
    private static string GetSheetTitle(ReportDocument document, IReadOnlyList<ReportSectionGroup> groups, int index)
    {
        var group = groups[index];

        if (!string.IsNullOrWhiteSpace(group.Container?.Title))
        {
            return group.Container.Title;
        }

        if (group.Container is null && groups.Count == 1 && !string.IsNullOrWhiteSpace(document.Title))
        {
            return document.Title;
        }

        if (group.Container is null && !string.IsNullOrWhiteSpace(group.Sections[0].Title))
        {
            return group.Sections[0].Title;
        }

        return string.IsNullOrWhiteSpace(document.Title) ? null : $"{document.Title} {index + 1}";
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

    private static void WriteSection(SheetData sheetData, ReportDocument document, ReportSection section, ExcelStyleRegistry styleRegistry)
    {
        var filters = document.GetFilteringSections(section);

        foreach (var filter in filters)
        {
            AppendTextRow(sheetData, FilterText(filter));
        }

        if (filters.Count > 0 && string.IsNullOrWhiteSpace(section.Description))
        {
            AppendEmptyRow(sheetData);
        }

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
                WriteTableSection(sheetData, section, styleRegistry);
                break;
            case ReportSectionKind.Bars:
                WriteBarsSection(sheetData, section);
                break;
            case ReportSectionKind.Chart:
                WriteChartSection(sheetData, section.Chart);
                break;
        }
    }

    // Says which values picked in another section filter this one, as that section shows them.
    private static string FilterText(ReportSection filter)
    {
        var values = string.Join(", ", filter.Selection.GetPickedValues());

        return string.IsNullOrWhiteSpace(filter.Title) ? $"Filtered by: {values}" : $"Filtered by {filter.Title}: {values}";
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

    private static void WriteTableSection(SheetData sheetData, ReportSection section, ExcelStyleRegistry styleRegistry)
    {
        if (section.Columns.Count > 0)
        {
            var headerRow = new Row();

            foreach (var column in section.Columns)
            {
                var headerStyle = EnsureBold(column.HeaderStyle);

                headerRow.Append(CreateCell(column.Label, styleRegistry.GetCellFormatIndex(headerStyle)));
            }

            sheetData.Append(headerRow);
        }

        foreach (var row in section.Rows)
        {
            var dataRow = new Row();
            var emphasize = row.Kind != ReportRowKind.Detail;

            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cellStyle = row.GetCellStyle(i);

                if (emphasize)
                {
                    cellStyle = EnsureBold(cellStyle);
                }

                dataRow.Append(CreateCell(row.Cells[i], styleRegistry.GetCellFormatIndex(cellStyle)));
            }

            sheetData.Append(dataRow);
        }
    }

    private static ReportStyle EnsureBold(ReportStyle style)
    {
        if (style is null)
        {
            return ReportStyle.Create(bold: true);
        }

        if (style.Bold)
        {
            return style;
        }

        return ReportStyle.Create(style.Color, style.BackgroundColor, bold: true);
    }

    private static Cell CreateCell(string value, uint styleIndex)
    {
        var cell = new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value ?? string.Empty)),
        };

        if (styleIndex != 0)
        {
            cell.StyleIndex = styleIndex;
        }

        return cell;
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

        // The values are written as numbers, so the sheet can chart, add up and sort them.
        for (var labelIndex = 0; labelIndex < chart.Labels.Count; labelIndex++)
        {
            var row = new Row(CreateCell(chart.Labels[labelIndex], 0));

            foreach (var dataset in chart.Datasets)
            {
                row.Append(labelIndex < dataset.Values.Count && double.IsFinite(dataset.Values[labelIndex])
                    ? new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(dataset.Values[labelIndex].ToString("R", CultureInfo.InvariantCulture)),
                    }
                    : CreateCell(string.Empty, 0));
            }

            sheetData.Append(row);
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
