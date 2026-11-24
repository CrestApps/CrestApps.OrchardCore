using System.Net;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Reads the rows of a skills file one at a time, in one of two layouts:
/// <list type="bullet">
/// <item>a Moodle competency framework export (the file Moodle's "Export competency framework" writes, known by its
/// <c>isframework</c> column): the framework row names the category, and every competency under it becomes a skill
/// named by its short name (or its ID number when it has none), its description turned into plain text;</item>
/// <item>a plain file with a <c>Name</c> column and optional <c>Category</c>, <c>Description</c> and <c>Enabled</c>
/// columns, in any case.</item>
/// </list>
/// The reader keeps the framework of a Moodle export from one row to the next in <see cref="Framework"/>.
/// </summary>
public sealed partial class SkillImportRowReader
{
    /// <summary>
    /// The longest name or category a skill can have.
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// Gets or sets the framework of a Moodle export, from its framework row, which the competencies under it go into.
    /// </summary>
    public string Framework { get; set; }

    /// <summary>
    /// Gets whether a file's columns are those of a Moodle competency framework export.
    /// </summary>
    /// <param name="columns">The column names.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    public static bool IsMoodleExport(IEnumerable<string> columns)
    {
        var names = new HashSet<string>(columns ?? [], StringComparer.OrdinalIgnoreCase);

        return names.Contains("isframework") && names.Contains("shortname");
    }

    /// <summary>
    /// Reads one row.
    /// </summary>
    /// <param name="line">The row's number, for the messages.</param>
    /// <param name="cells">The cells by column name, in any case.</param>
    /// <returns>A skill, a Moodle framework row, or why the row cannot be read.</returns>
    public SkillImportRowResult Read(int line, IReadOnlyDictionary<string, string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        string Cell(string column)
            => cells.TryGetValue(column, out var value) ? Clean(value) : null;

        if (IsMoodleExport(cells.Keys))
        {
            if (IsTrue(Cell("isframework")))
            {
                Framework = Cell("shortname") ?? Cell("idnumber");

                return Framework is null
                    ? SkillImportRowResult.Failure($"Line {line}: the framework has neither a short name nor an ID number.")
                    : Framework.Length > MaxLength
                        ? SkillImportRowResult.Failure($"Line {line}: the framework name is longer than {MaxLength} characters.")
                        : SkillImportRowResult.FrameworkRow(Framework);
            }

            if (Framework is null)
            {
                return SkillImportRowResult.Failure($"Line {line} comes before the framework row.");
            }

            var name = Cell("shortname") ?? Cell("idnumber");

            if (name is null)
            {
                return SkillImportRowResult.Failure($"Line {line} has neither a short name nor an ID number.");
            }

            // Moodle's plain text (2) and Markdown (4) formats are kept; HTML (1) and the Moodle auto-format (0) may
            // hold tags.
            var description = Cell("descriptionformat") is "2" or "4"
                ? Cell("description")
                : ToPlainText(Cell("description"));

            return Skill(line, name, Framework, description, null);
        }

        var plainName = Cell("Name");

        if (plainName is null)
        {
            return SkillImportRowResult.Failure($"Line {line} has no name.");
        }

        if (!TryReadEnabled(Cell("Enabled"), out var enabled))
        {
            return SkillImportRowResult.Failure($"Line {line}: '{Cell("Enabled")}' is not yes or no in the Enabled column.");
        }

        return Skill(line, plainName, Cell("Category"), Cell("Description"), enabled);
    }

    /// <summary>
    /// Turns HTML into plain text: the tags removed, the entities decoded and the spaces collapsed.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
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

    private static SkillImportRowResult Skill(int line, string name, string category, string description, bool? enabled)
    {
        if (name.Length > MaxLength || category?.Length > MaxLength)
        {
            return SkillImportRowResult.Failure($"Line {line}: a name or a category is longer than {MaxLength} characters.");
        }

        return SkillImportRowResult.ForSkill(new SkillImportRow
        {
            Line = line,
            Name = name,
            Category = category,
            Description = description,
            Enabled = enabled,
        });
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTrue(string value)
        => value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadEnabled(string value, out bool? enabled)
    {
        enabled = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (value is "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            enabled = true;

            return true;
        }

        if (value is "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("n", StringComparison.OrdinalIgnoreCase))
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

/// <summary>
/// What a row of a skills file holds.
/// </summary>
public sealed class SkillImportRowResult
{
    private SkillImportRowResult()
    {
    }

    /// <summary>
    /// Gets the skill, or <see langword="null"/> when the row holds none.
    /// </summary>
    public SkillImportRow Skill { get; private init; }

    /// <summary>
    /// Gets the framework a Moodle framework row names, or <see langword="null"/>.
    /// </summary>
    public string Framework { get; private init; }

    /// <summary>
    /// Gets why the row cannot be read, or <see langword="null"/>.
    /// </summary>
    public string Error { get; private init; }

    internal static SkillImportRowResult ForSkill(SkillImportRow skill) => new() { Skill = skill };

    internal static SkillImportRowResult FrameworkRow(string framework) => new() { Framework = framework };

    internal static SkillImportRowResult Failure(string error) => new() { Error = error };
}
