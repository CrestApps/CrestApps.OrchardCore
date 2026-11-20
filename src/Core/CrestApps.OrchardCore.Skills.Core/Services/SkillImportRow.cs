namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// A skill read from an import file.
/// </summary>
public sealed class SkillImportRow
{
    /// <summary>
    /// Gets or sets the row's line in the file, counting the header as 1.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Gets or sets the skill name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category the skill is listed under: the competency framework of a Moodle export.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets what the skill means, as plain text.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the skill can be given to people, or <see langword="null"/> when the file doesn't say.
    /// </summary>
    public bool? Enabled { get; set; }
}

/// <summary>
/// What an import did.
/// </summary>
public sealed class SkillImportResult
{
    /// <summary>
    /// Gets or sets how many skills were added.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Gets or sets how many existing skills were changed.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets or sets how many existing skills already matched the file.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// Gets the rows that were left out, by line, with why.
    /// </summary>
    public Dictionary<int, string> Failures { get; } = [];
}
