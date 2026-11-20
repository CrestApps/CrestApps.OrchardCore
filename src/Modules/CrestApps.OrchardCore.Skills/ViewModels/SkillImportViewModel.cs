using CrestApps.OrchardCore.Skills.Core.Services;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The page that imports skills from a CSV file, and what the last import did.
/// </summary>
public class SkillImportViewModel
{
    /// <summary>
    /// Gets or sets the format the file was read as, or <see langword="null"/> before an import.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the rows that could not be read.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets what the import did, or <see langword="null"/> before an import.
    /// </summary>
    public SkillImportResult Result { get; set; }
}
