namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What an import added to a bank.
/// </summary>
public sealed class QuestionBankImportSummary
{
    /// <summary>
    /// Gets or sets how many questions were added.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Gets or sets how many questions with a known source identifier were updated.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets the localized problems found.
    /// </summary>
    public List<string> Errors { get; } = [];
}
