using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Reads quizzes and questions: the latest published versions, and the questions of a bank.
/// </summary>
public interface IAssessmentContentSource
{
    /// <summary>
    /// Gets the published version of a content item.
    /// </summary>
    /// <param name="contentItemId">The content item.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    Task<ContentItem> GetItemAsync(string contentItemId);

    /// <summary>
    /// Gets published questions, in the order asked.
    /// </summary>
    /// <param name="questionIds">The questions.</param>
    /// <returns>The questions found.</returns>
    Task<IReadOnlyList<ContentItem>> GetQuestionsAsync(IEnumerable<string> questionIds);

    /// <summary>
    /// Gets the published questions of a bank.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <returns>The questions.</returns>
    Task<IReadOnlyList<ContentItem>> GetBankQuestionsAsync(string bankId);
}
