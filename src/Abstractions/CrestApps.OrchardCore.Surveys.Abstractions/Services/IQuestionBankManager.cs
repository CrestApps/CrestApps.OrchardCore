using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Creates, changes and removes question banks and their questions. Every save of a bank question increases its revision,
/// so surveys can tell when a question they drew changed after they were published.
/// </summary>
public interface IQuestionBankManager
{
    /// <summary>
    /// Makes a new bank that is not saved yet, with its audit fields set.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new bank.</returns>
    Task<QuestionBank> NewBankAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a bank. The technical name must be unique and the title set.
    /// </summary>
    /// <param name="bank">The bank.</param>
    /// <param name="isNew">Whether the bank is new.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<QuestionBankSaveResult> SaveBankAsync(QuestionBank bank, bool isNew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bank and its questions. Published survey versions keep the questions they drew, since a version holds
    /// its own copy.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the bank was deleted.</returns>
    Task<bool> DeleteBankAsync(string bankId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a new bank question that is not saved yet, with its audit fields set and its question id equal to its id.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="controlType">The question type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new question.</returns>
    Task<BankQuestion> NewQuestionAsync(string bankId, string controlType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a bank question and increases its revision.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="isNew">Whether the question is new.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<QuestionBankSaveResult> SaveQuestionAsync(BankQuestion question, bool isNew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires questions so pools no longer draw them, or brings them back.
    /// </summary>
    /// <param name="questionIds">The questions.</param>
    /// <param name="retired">Whether they are retired.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many questions changed.</returns>
    Task<int> RetireQuestionsAsync(IEnumerable<string> questionIds, bool retired, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves questions to another bank.
    /// </summary>
    /// <param name="questionIds">The questions.</param>
    /// <param name="targetBankId">The bank they move to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many questions moved.</returns>
    Task<int> MoveQuestionsAsync(IEnumerable<string> questionIds, string targetBankId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes questions.
    /// </summary>
    /// <param name="questionIds">The questions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many questions were deleted.</returns>
    Task<int> DeleteQuestionsAsync(IEnumerable<string> questionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds imported questions to a bank. A question whose source identifier is already in the bank updates that
    /// question instead of adding a copy.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="questions">The questions read from a file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What was added and updated.</returns>
    Task<QuestionBankImportSummary> ImportAsync(string bankId, IEnumerable<BankQuestion> questions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks imported questions against a bank without saving anything: what <see cref="ImportAsync"/> would add and
    /// update, the questions it would refuse, and each choice or row it would match by its place only (its text changed),
    /// which then keeps the earlier item's answers and key.
    /// </summary>
    /// <param name="bankId">The bank.</param>
    /// <param name="questions">The questions read from a file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the import would add and update, with its errors and notices.</returns>
    Task<QuestionBankImportSummary> CheckImportAsync(string bankId, IEnumerable<BankQuestion> questions, CancellationToken cancellationToken = default);
}
