namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of saving a bank or a bank question.
/// </summary>
public sealed class QuestionBankSaveResult
{
    /// <summary>
    /// Gets the localized problems found; empty when the save succeeded.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets whether the save succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
