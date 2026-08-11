using CrestApps.OrchardCore.Reports.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.Reports.OpenXml.Services;

/// <summary>
/// Collects the distinct fonts, fills, and cell formats required by a report and produces the Open XML
/// <see cref="Stylesheet"/> that backs them. Each <see cref="ReportStyle"/> is deduplicated and mapped to
/// a cell-format index that is assigned to the relevant worksheet cells.
/// </summary>
internal sealed class ExcelStyleRegistry
{
    private const string DefaultFontName = "Calibri";
    private const double DefaultFontSize = 11d;

    private readonly List<Font> _fonts = [];
    private readonly List<Fill> _fills = [];
    private readonly List<CellFormat> _cellFormats = [];
    private readonly Dictionary<string, uint> _fontIndexByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _fillIndexByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _cellFormatIndexByKey = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelStyleRegistry"/> class and seeds the mandatory
    /// default font, fills, and cell format required by the Open XML spreadsheet format.
    /// </summary>
    public ExcelStyleRegistry()
    {
        _fonts.Add(CreateFont(color: null, bold: false));
        _fills.Add(new Fill(new PatternFill { PatternType = PatternValues.None }));
        _fills.Add(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        _cellFormats.Add(new CellFormat());
    }

    /// <summary>
    /// Resolves the cell-format index for the supplied style, creating the backing font, fill, and cell
    /// format on demand.
    /// </summary>
    /// <param name="style">The style to resolve. May be <see langword="null"/>.</param>
    /// <returns>The cell-format index, or <c>0</c> (the default format) when the style has no effect.</returns>
    public uint GetCellFormatIndex(ReportStyle style)
    {
        if (style is null || style.IsEmpty)
        {
            return 0;
        }

        var fontId = GetFontIndex(style);
        var fillId = GetFillIndex(style);
        var key = $"{fontId}:{fillId}";

        if (_cellFormatIndexByKey.TryGetValue(key, out var index))
        {
            return index;
        }

        _cellFormats.Add(new CellFormat
        {
            FontId = fontId,
            FillId = fillId,
            ApplyFont = fontId != 0,
            ApplyFill = fillId != 0,
        });

        index = (uint)(_cellFormats.Count - 1);
        _cellFormatIndexByKey[key] = index;

        return index;
    }

    /// <summary>
    /// Builds the Open XML stylesheet from every font, fill, and cell format registered so far.
    /// </summary>
    /// <returns>The stylesheet.</returns>
    public Stylesheet Build()
    {
        return new Stylesheet(
            new Fonts(_fonts.Select(font => font.CloneNode(true))) { Count = (uint)_fonts.Count },
            new Fills(_fills.Select(fill => fill.CloneNode(true))) { Count = (uint)_fills.Count },
            new Borders(new Border()) { Count = 1 },
            new CellStyleFormats(new CellFormat()) { Count = 1 },
            new CellFormats(_cellFormats.Select(cellFormat => cellFormat.CloneNode(true))) { Count = (uint)_cellFormats.Count });
    }

    private uint GetFontIndex(ReportStyle style)
    {
        var hasColor = ReportColor.TryGetArgb(style.Color, out var argb);

        if (!hasColor && !style.Bold)
        {
            return 0;
        }

        var key = $"{(hasColor ? argb : string.Empty)}|{style.Bold}";

        if (_fontIndexByKey.TryGetValue(key, out var index))
        {
            return index;
        }

        _fonts.Add(CreateFont(hasColor ? argb : null, style.Bold));
        index = (uint)(_fonts.Count - 1);
        _fontIndexByKey[key] = index;

        return index;
    }

    private uint GetFillIndex(ReportStyle style)
    {
        if (!ReportColor.TryGetArgb(style.BackgroundColor, out var argb))
        {
            return 0;
        }

        if (_fillIndexByKey.TryGetValue(argb, out var index))
        {
            return index;
        }

        _fills.Add(new Fill(new PatternFill(new ForegroundColor { Rgb = new HexBinaryValue(argb) })
        {
            PatternType = PatternValues.Solid,
        }));

        index = (uint)(_fills.Count - 1);
        _fillIndexByKey[argb] = index;

        return index;
    }

    private static Font CreateFont(string color, bool bold)
    {
        var font = new Font();

        if (bold)
        {
            font.Append(new Bold());
        }

        font.Append(new FontSize { Val = DefaultFontSize });

        if (color is not null)
        {
            font.Append(new Color { Rgb = new HexBinaryValue(color) });
        }

        font.Append(new FontName { Val = DefaultFontName });

        return font;
    }
}
