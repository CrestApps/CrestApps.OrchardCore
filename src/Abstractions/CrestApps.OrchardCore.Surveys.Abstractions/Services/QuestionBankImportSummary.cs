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

    /// <summary>
    /// Gets the localized notes the author should check, such as a choice of an updated question that kept the id (and
    /// the earlier answers) of the choice at its place although its text changed.
    /// </summary>
    public List<string> Notices { get; } = [];
}
