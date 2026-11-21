using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The questions read from a file and the problems found.
/// </summary>
public sealed class SurveyImportResult
{
    /// <summary>
    /// Gets or sets the questions read, not saved yet.
    /// </summary>
    public List<BankQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the problems found, each naming the row or item.
    /// </summary>
    public List<string> Errors { get; set; } = [];
}
