using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Reads skills from a CSV file, in one of two formats:
/// <list type="bullet">
/// <item>a Moodle competency framework export (the file Moodle's "Export competency framework" writes, known by its
/// <c>isframework</c> column): the framework row names the category, and every competency under it becomes a skill
/// named by its short name (or its ID number when it has none), its description turned into plain text;</item>
/// <item>a plain file with a <c>Name</c> column and optional <c>Category</c>, <c>Description</c> and <c>Enabled</c>
/// columns, in any case.</item>
/// </list>
/// </summary>
public static partial class SkillCsvImport
{
    /// <summary>
    /// A Moodle competency framework export.
    /// </summary>
    public const string MoodleFormat = "Moodle";

    /// <summary>
    /// A plain file with Name, Category, Description and Enabled columns.
    /// </summary>
    public const string PlainFormat = "Plain";

    private const int MaxLength = 255;

    /// <summary>
    /// Reads the skills of a CSV text.
    /// </summary>
    /// <param name="text">The CSV text.</param>
    /// <returns>The skills read, and why rows could not be read.</returns>
    public static SkillCsvParseResult Parse(string text)
    {
        var (headers, rows) = CsvReader.Parse(text);
        var result = new SkillCsvParseResult();

        if (Has(headers, "isframework") && Has(headers, "shortname"))
        {
            result.Format = MoodleFormat;
            ReadMoodle(rows, result);

            return result;
        }

        result.Format = PlainFormat;

        if (!Has(headers, "Name"))
        {
            result.Errors.Add("The file has no Name column. Use a Moodle competency framework export, or a file with Name, Category, Description and Enabled columns.");

            return result;
        }

        foreach (var row in rows)
        {
            var name = Clean(row["Name"]);

            if (name is null)
            {
                result.Errors.Add($"Line {row.Number} has no name.");

                continue;
            }

            if (!TryReadEnabled(row["Enabled"], out var enabled))
            {
                result.Errors.Add($"Line {row.Number}: '{row["Enabled"]}' is not yes or no in the Enabled column.");

                continue;
            }

            Add(result, row.Number, name, Clean(row["Category"]), Clean(row["Description"]), enabled);
        }

        return result;
    }

    private static void ReadMoodle(IReadOnlyList<CsvRow> rows, SkillCsvParseResult result)
    {
        string framework = null;

        foreach (var row in rows)
        {
            if (IsTrue(row["isframework"]))
            {
                framework = Clean(row["shortname"]) ?? Clean(row["idnumber"]);

                continue;
            }

            if (framework is null)
            {
                result.Errors.Add($"Line {row.Number} comes before the framework row.");

                continue;
            }

            var name = Clean(row["shortname"]) ?? Clean(row["idnumber"]);

            if (name is null)
            {
                result.Errors.Add($"Line {row.Number} has neither a short name nor an ID number.");

                continue;
            }

            // Moodle's plain text (2) and Markdown (4) formats are kept; HTML (1) and the Moodle auto-format (0) may
            // hold tags.
            var description = row["descriptionformat"]?.Trim() is "2" or "4"
                ? Clean(row["description"])
                : ToPlainText(row["description"]);

            Add(result, row.Number, name, framework, description, null);
        }

        if (framework is null && result.Errors.Count == 0)
        {
            result.Errors.Add("The file has no framework row (isframework = 1).");
        }
    }

    private static void Add(SkillCsvParseResult result, int line, string name, string category, string description, bool? enabled)
    {
        if (name.Length > MaxLength || category?.Length > MaxLength)
        {
            result.Errors.Add($"Line {line}: a name or a category is longer than {MaxLength} characters.");

            return;
        }

        result.Rows.Add(new SkillImportRow
        {
            Line = line,
            Name = name,
            Category = category,
            Description = description,
            Enabled = enabled,
        });
    }

    /// <summary>
    /// Turns HTML into plain text: the tags removed, the entities decoded and the spaces collapsed.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The text.</returns>
    public static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(Tags().Replace(html, " "));

        var plain = Spaces().Replace(text, " ").Trim();

        return plain.Length == 0 ? null : plain;
    }

    private static bool Has(IReadOnlyList<string> headers, string column)
        => headers.Any(header => string.Equals(header, column, StringComparison.OrdinalIgnoreCase));

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTrue(string value)
        => value?.Trim() is "1" || string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadEnabled(string value, out bool? enabled)
    {
        enabled = null;

        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (text is "1" || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;

            return true;
        }

        if (text is "0" || text.Equals("no", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase) || text.Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;

            return true;
        }

        return false;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
