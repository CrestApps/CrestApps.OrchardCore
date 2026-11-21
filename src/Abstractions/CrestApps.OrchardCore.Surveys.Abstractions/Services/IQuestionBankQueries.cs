using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads question banks and their questions for staff pages and pickers.
/// </summary>
public interface IQuestionBankQueries
{
    /// <summary>
    /// Reads a page of banks, ordered by title.
    /// </summary>
    /// <param name="search">A search on the title or name, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    Task<(int Count, IReadOnlyList<QuestionBank> Items)> ListBanksAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of bank questions.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    Task<(int Count, IReadOnlyList<BankQuestion> Items)> ListQuestionsAsync(BankQuestionQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the distinct tags of a bank's questions.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tags, sorted.</returns>
    Task<IReadOnlyList<string>> ListTagsAsync(string bankId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the questions of a bank.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count, retired questions included.</returns>
    Task<int> CountQuestionsAsync(string bankId, CancellationToken cancellationToken = default);
}
