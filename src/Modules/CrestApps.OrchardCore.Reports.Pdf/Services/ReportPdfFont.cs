using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Fonts.TrueType;
using UglyToad.PdfPig.Fonts.TrueType.Parser;
using UglyToad.PdfPig.Graphics.Operations.General;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// One face of the font of a report PDF, measured and written with PdfPig: an installed TrueType font, embedded as a
/// subset of the characters the report uses, or Helvetica, one of the fonts every PDF reader has, when the host has
/// no usable font.
/// </summary>
/// <remarks>
/// A character the font has no glyph for is written as a question mark; Helvetica writes the letters of ASCII, so an
/// accented letter is written without its accent. A PdfPig TrueType font holds up to 255 different characters, so
/// the font is embedded again for the next 255.
/// </remarks>
internal sealed class ReportPdfFont
{
    private const int CharactersPerFont = 255;

    // Helvetica and the fonts that stand in for Arial have about the same vertical metrics.
    private const double StandardAscent = 0.905;
    private const double StandardLineHeight = 1.15;

    // The characters every font must have, so any text can be written with it once its other characters are replaced.
    private const string RequiredCharacters = " ?.,-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private readonly PdfDocumentBuilder _builder;
    private readonly byte[] _file;
    private readonly TrueTypeFont _trueType;
    private readonly double _unitsPerEm;
    private readonly Standard14Font _standardFont;
    private readonly Dictionary<char, double> _standardWidths;
    private readonly Dictionary<char, double> _widths = [];
    private readonly List<EmbeddedFont> _embedded = [];

    private ReportPdfFont(PdfDocumentBuilder builder, byte[] file, TrueTypeFont trueType, bool simulatesBold)
    {
        _builder = builder;
        _file = file;
        _trueType = trueType;
        _unitsPerEm = trueType.GetUnitsPerEm();
        SimulatesBold = simulatesBold;

        var header = trueType.TableRegister.HorizontalHeaderTable;
        Ascent = header.Ascent / _unitsPerEm;
        LineHeight = (header.Ascent - header.Descent + header.LineGap) / _unitsPerEm;

        if (Ascent <= 0 || LineHeight < Ascent || LineHeight > 2)
        {
            Ascent = StandardAscent;
            LineHeight = StandardLineHeight;
        }
    }

    private ReportPdfFont(PdfDocumentBuilder builder, Standard14Font standardFont)
    {
        _builder = builder;
        _standardFont = standardFont;
        _standardWidths = [];
        Ascent = StandardAscent;
        LineHeight = StandardLineHeight;

        var metrics = Standard14.GetAdobeFontMetrics(standardFont);

        // PdfPig writes Helvetica in the standard encoding, where the quote and the backtick have other codes.
        for (var character = ' '; character <= '~'; character++)
        {
            var code = character switch
            {
                '\'' => 169,
                '`' => 193,
                _ => character,
            };

            var metric = metrics.CharacterMetrics.Values.FirstOrDefault(value => value.CharacterCode == code);

            if (metric is not null)
            {
                _standardWidths[character] = metric.Width.X / 1000;
            }
        }
    }

    /// <summary>
    /// Gets the distance from the top of a line to its baseline, as a fraction of the font size.
    /// </summary>
    public double Ascent { get; }

    /// <summary>
    /// Gets the height of a line, as a fraction of the font size.
    /// </summary>
    public double LineHeight { get; }

    /// <summary>
    /// Gets a value indicating whether the face is bold by outlining the regular face, because the font has no bold
    /// face.
    /// </summary>
    public bool SimulatesBold { get; }

    /// <summary>
    /// Creates a face from a TrueType font file.
    /// </summary>
    /// <param name="builder">The document the face writes in.</param>
    /// <param name="file">The TrueType file.</param>
    /// <param name="simulatesBold">Whether to write the face bold by outlining it.</param>
    /// <returns>The face, or <see langword="null"/> when PdfPig cannot write with the font.</returns>
    public static ReportPdfFont FromTrueType(PdfDocumentBuilder builder, byte[] file, bool simulatesBold)
    {
        var trueType = Parse(file);

        return trueType is null
            ? null
            : new ReportPdfFont(builder, file, trueType, simulatesBold);
    }

    /// <summary>
    /// Creates a face from one of the fonts every PDF reader has.
    /// </summary>
    /// <param name="builder">The document the face writes in.</param>
    /// <param name="font">The font.</param>
    /// <returns>The face.</returns>
    public static ReportPdfFont FromStandard(PdfDocumentBuilder builder, Standard14Font font)
    {
        return new ReportPdfFont(builder, font);
    }

    /// <summary>
    /// Checks that PdfPig can write with a TrueType font file, and that the font has the characters of plain text.
    /// </summary>
    /// <param name="file">The TrueType file.</param>
    /// <returns><see langword="true"/> when the font can be used.</returns>
    public static bool CanUse(byte[] file)
    {
        return Parse(file) is not null;
    }

    /// <summary>
    /// Prepares a value to be measured and written: line breaks are kept as <c>\n</c>, other control characters
    /// become spaces, and characters the font has no glyph for are replaced.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text the font can write.</returns>
    public string Prepare(string value)
    {
        var text = ReportPdfText.Clean(value);

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = null;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (character == '\n' || GetWidth(character) is not null)
            {
                builder?.Append(character);

                continue;
            }

            builder ??= new StringBuilder(text.Length).Append(text, 0, index);

            // A character outside the first plane is a pair of chars, and is replaced once.
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }

            builder.Append(Substitute(character));
        }

        return builder?.ToString() ?? text;
    }

    /// <summary>
    /// Gets whether the font has a glyph for a character.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> when the font can write the character.</returns>
    public bool Supports(char character)
    {
        return GetWidth(character) is not null;
    }

    /// <summary>
    /// Measures a line of prepared text.
    /// </summary>
    /// <param name="text">The text, from <see cref="Prepare"/>.</param>
    /// <param name="size">The font size, in points.</param>
    /// <returns>The width, in points.</returns>
    public double Measure(string text, double size)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0d;

        foreach (var character in text)
        {
            width += GetWidth(character) ?? 0;
        }

        return width * size;
    }

    /// <summary>
    /// Measures a prepared character.
    /// </summary>
    /// <param name="character">The character, from text <see cref="Prepare"/> returned.</param>
    /// <param name="size">The font size, in points.</param>
    /// <returns>The width, in points.</returns>
    public double Measure(char character, double size)
    {
        return (GetWidth(character) ?? 0) * size;
    }

    /// <summary>
    /// Writes a line of prepared text. The color of the text is the fill color of the page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="text">The text, from <see cref="Prepare"/>, without line breaks.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="x">The left of the text, in points from the left of the page.</param>
    /// <param name="baseline">The baseline of the text, in points from the bottom of the page.</param>
    public void Write(PdfPageBuilder page, string text, double size, double x, double baseline)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (SimulatesBold)
        {
            page.SetTextRenderingMode(TextRenderingMode.FillThenStroke);
            page.CurrentStream.Operations.Add(new SetLineWidth(size / 30));
        }

        var start = 0;

        while (start < text.Length)
        {
            var (font, length) = TakeRun(text, start);
            var run = text.Substring(start, length);

            page.AddText(run, size, new PdfPoint(x, baseline), font);
            x += Measure(run, size);
            start += length;
        }

        if (SimulatesBold)
        {
            page.SetTextRenderingMode(TextRenderingMode.Fill);
        }
    }

    private (PdfDocumentBuilder.AddedFont Font, int Length) TakeRun(string text, int start)
    {
        if (_file is null)
        {
            if (_embedded.Count == 0)
            {
                _embedded.Add(new EmbeddedFont(_builder.AddStandard14Font(_standardFont)));
            }

            return (_embedded[0].Font, text.Length - start);
        }

        var embedded = _embedded.FirstOrDefault(font => font.Characters.Contains(text[start]))
            ?? _embedded.FirstOrDefault(font => font.Characters.Count < CharactersPerFont);

        if (embedded is null)
        {
            // Fonts are embedded when they write their first character: a font that writes nothing is not embedded.
            embedded = new EmbeddedFont(_builder.AddTrueTypeFont(_file));
            _embedded.Add(embedded);
        }

        var length = 0;

        while (start + length < text.Length)
        {
            var character = text[start + length];

            if (!embedded.Characters.Contains(character))
            {
                if (embedded.Characters.Count >= CharactersPerFont)
                {
                    break;
                }

                embedded.Characters.Add(character);
            }

            length++;
        }

        return (embedded.Font, length);
    }

    private char Substitute(char character)
    {
        if (char.IsWhiteSpace(character))
        {
            return ' ';
        }

        if (char.IsSurrogate(character))
        {
            return '?';
        }

        // An accented letter is written without its accent when the font has the letter, as Helvetica has.
        var decomposed = character.ToString().Normalize(NormalizationForm.FormD);

        if (decomposed.Length > 1 && !char.IsSurrogate(decomposed[0]) && GetWidth(decomposed[0]) is not null)
        {
            return decomposed[0];
        }

        return '?';
    }

    private double? GetWidth(char character)
    {
        if (_standardWidths is not null)
        {
            return _standardWidths.TryGetValue(character, out var standardWidth)
                ? standardWidth
                : null;
        }

        if (!_widths.TryGetValue(character, out var width))
        {
            width = GetTrueTypeWidth(_trueType, character) is { } units ? units / _unitsPerEm : double.NaN;
            _widths[character] = width;
        }

        return double.IsNaN(width) ? null : width;
    }

    private static double? GetTrueTypeWidth(TrueTypeFont font, char character)
    {
        // PdfPig writes a character only when its glyph has a box and a width; one the font maps to no glyph would be
        // written as an empty box.
        if (char.IsSurrogate(character))
        {
            return null;
        }

        try
        {
            if (font.WindowsUnicodeCMap.CharacterCodeToGlyphIndex(character) == 0 ||
                !font.TryGetBoundingBox(character, out _) ||
                !font.TryGetAdvanceWidth(character, out var width))
            {
                return null;
            }

            return width;
        }
        catch (Exception)
        {
            // A glyph PdfPig cannot read is treated as missing, and replaced like one.
            return null;
        }
    }

    private static TrueTypeFont Parse(byte[] file)
    {
        if (file is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var font = TrueTypeFontParser.Parse(new TrueTypeDataBytes(file));
            var tables = font.TableRegister;

            // The tables PdfPig reads to embed the font.
            if (tables.CMapTable is null ||
                tables.Os2Table is null ||
                tables.PostScriptTable is null ||
                tables.NameTable is null ||
                tables.HorizontalHeaderTable is null ||
                tables.HorizontalMetricsTable is null ||
                font.WindowsUnicodeCMap is null ||
                font.GetUnitsPerEm() <= 0)
            {
                return null;
            }

            return RequiredCharacters.All(character => GetTrueTypeWidth(font, character) is not null)
                ? font
                : null;
        }
        catch (Exception)
        {
            // A font PdfPig cannot read is not used; another font, or Helvetica, writes the report.
            return null;
        }
    }

    private sealed class EmbeddedFont
    {
        public EmbeddedFont(PdfDocumentBuilder.AddedFont font)
        {
            Font = font;
        }

        public PdfDocumentBuilder.AddedFont Font { get; }

        public HashSet<char> Characters { get; } = [];
    }
}
