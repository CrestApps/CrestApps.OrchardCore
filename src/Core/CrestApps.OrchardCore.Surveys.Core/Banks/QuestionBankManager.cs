using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Creates, changes and removes question banks and their questions.
/// </summary>
public sealed partial class QuestionBankManager : IQuestionBankManager
{
    private readonly QuestionBankCatalogManager _banks;
    private readonly BankQuestionCatalogManager _questions;
    private readonly QuestionBankStore _bankStore;
    private readonly BankQuestionStore _questionStore;
    private readonly ISurveyControlRegistry _controls;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankManager"/> class.
    /// </summary>
    /// <param name="banks">The bank catalog manager.</param>
    /// <param name="questions">The bank question catalog manager.</param>
    /// <param name="bankStore">The bank store.</param>
    /// <param name="questionStore">The bank question store.</param>
    /// <param name="controls">The question type registry.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuestionBankManager(
        QuestionBankCatalogManager banks,
        BankQuestionCatalogManager questions,
        QuestionBankStore bankStore,
        BankQuestionStore questionStore,
        ISurveyControlRegistry controls,
        IStringLocalizer<QuestionBankManager> stringLocalizer)
    {
        _banks = banks;
        _questions = questions;
        _bankStore = bankStore;
        _questionStore = questionStore;
        _controls = controls;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<QuestionBank> NewBankAsync(CancellationToken cancellationToken = default)
        => await _banks.NewAsync(null, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestionBankSaveResult> SaveBankAsync(QuestionBank bank, bool isNew, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bank);

        var result = new QuestionBankSaveResult();

        bank.Name = bank.Name?.Trim().ToLowerInvariant();
        bank.Title = bank.Title?.Trim();
        bank.Tags = (bank.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrEmpty(bank.Title))
        {
            result.Errors.Add(S["The title is required."]);
        }

        if (string.IsNullOrEmpty(bank.Name))
        {
            result.Errors.Add(S["The technical name is required."]);
        }
        else if (!NamePattern().IsMatch(bank.Name))
        {
            result.Errors.Add(S["The technical name may only hold lowercase letters, digits and dashes."]);
        }
        else
        {
            var existing = await _bankStore.FindByNameAsync(bank.Name, cancellationToken);

            if (existing is not null && existing.ItemId != bank.ItemId)
            {
                result.Errors.Add(S["Another question bank already uses the technical name '{0}'.", bank.Name]);
            }
        }

        if (!result.Succeeded)
        {
            return result;
        }

        if (isNew)
        {
            await _banks.CreateAsync(bank, cancellationToken);
        }
        else
        {
            await _banks.UpdateAsync(bank, null, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBankAsync(string bankId, CancellationToken cancellationToken = default)
    {
        var bank = await _bankStore.FindByIdAsync(bankId, cancellationToken);

        if (bank is null)
        {
            return false;
        }

        foreach (var question in await _questionStore.ListByBankAsync(bankId, includeRetired: true, cancellationToken))
        {
            await _questions.DeleteAsync(question, cancellationToken);
        }

        return await _banks.DeleteAsync(bank, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BankQuestion> NewQuestionAsync(string bankId, string controlType, CancellationToken cancellationToken = default)
    {
        var question = await _questions.NewAsync(null, cancellationToken);
        question.BankId = bankId;
        question.Question ??= new SurveyQuestion();
        question.Question.Id = question.ItemId;
        question.Question.ControlType = controlType;

        if (_controls.GetHandler(controlType) is { } handler)
        {
            handler.Initialize(question.Question);
        }

        return question;
    }

    /// <inheritdoc />
    public async Task<QuestionBankSaveResult> SaveQuestionAsync(BankQuestion question, bool isNew, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);

        var result = await ValidateQuestionAsync(question, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        Normalize(question);
        question.Revision++;

        if (isNew)
        {
            await _questions.CreateAsync(question, cancellationToken);
        }
        else
        {
            await _questions.UpdateAsync(question, null, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<int> RetireQuestionsAsync(IEnumerable<string> questionIds, bool retired, CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var question in await LoadAsync(questionIds, cancellationToken))
        {
            if (question.Retired == retired)
            {
                continue;
            }

            question.Retired = retired;
            question.Revision++;
            await _questions.UpdateAsync(question, null, cancellationToken);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<int> MoveQuestionsAsync(IEnumerable<string> questionIds, string targetBankId, CancellationToken cancellationToken = default)
    {
        if (await _bankStore.FindByIdAsync(targetBankId, cancellationToken) is null)
        {
            return 0;
        }

        var count = 0;

        foreach (var question in await LoadAsync(questionIds, cancellationToken))
        {
            if (question.BankId == targetBankId)
            {
                continue;
            }

            question.BankId = targetBankId;
            question.Revision++;
            await _questions.UpdateAsync(question, null, cancellationToken);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<int> DeleteQuestionsAsync(IEnumerable<string> questionIds, CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var question in await LoadAsync(questionIds, cancellationToken))
        {
            if (await _questions.DeleteAsync(question, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<QuestionBankImportSummary> ImportAsync(string bankId, IEnumerable<BankQuestion> questions, CancellationToken cancellationToken = default)
    {
        var summary = new QuestionBankImportSummary();

        if (await _bankStore.FindByIdAsync(bankId, cancellationToken) is null)
        {
            summary.Errors.Add(S["The question bank does not exist."]);

            return summary;
        }

        var position = 0;

        foreach (var imported in questions ?? [])
        {
            position++;

            if (imported?.Question is null)
            {
                continue;
            }

            var existing = string.IsNullOrEmpty(imported.SourceIdentifier)
                ? null
                : await _questionStore.FindBySourceAsync(bankId, imported.SourceIdentifier, cancellationToken);

            var target = existing ?? await NewQuestionAsync(bankId, imported.Question.ControlType, cancellationToken);

            // The bank question keeps its id; the question inside takes it, so versions that drew it keep matching.
            target.Question = imported.Question;
            target.Question.Id = target.ItemId;
            target.Tags = imported.Tags ?? [];
            target.Difficulty = imported.Difficulty;
            target.SourceIdentifier = imported.SourceIdentifier;
            target.Retired = false;

            var result = await SaveQuestionAsync(target, existing is null, cancellationToken);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    summary.Errors.Add(S["Question {0}: {1}", position, error]);
                }

                continue;
            }

            if (existing is null)
            {
                summary.Created++;
            }
            else
            {
                summary.Updated++;
            }
        }

        return summary;
    }

    private async Task<QuestionBankSaveResult> ValidateQuestionAsync(BankQuestion question, CancellationToken cancellationToken)
    {
        var result = new QuestionBankSaveResult();

        if (string.IsNullOrEmpty(question.BankId) || await _bankStore.FindByIdAsync(question.BankId, cancellationToken) is null)
        {
            result.Errors.Add(S["The question bank does not exist."]);
        }

        if (question.Question is null || string.IsNullOrWhiteSpace(question.Question.ControlType))
        {
            result.Errors.Add(S["The question type is required."]);
        }
        else if (_controls.GetControl(question.Question.ControlType) is null)
        {
            result.Errors.Add(S["The question type '{0}' is not available.", question.Question.ControlType]);
        }

        if (string.IsNullOrWhiteSpace(question.Question?.Title))
        {
            result.Errors.Add(S["The question text is required."]);
        }

        if (question.Difficulty is < 1 or > 5)
        {
            result.Errors.Add(S["The difficulty must be from 1 to 5."]);
        }

        return result;
    }

    private static void Normalize(BankQuestion question)
    {
        question.Question.Id = question.ItemId;
        question.Question.PoolId = null;
        question.Tags = (question.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(question.Question.Name))
        {
            question.Question.Name = "q_" + question.ItemId.ToLowerInvariant();
        }
    }

    private async Task<IReadOnlyList<BankQuestion>> LoadAsync(IEnumerable<string> questionIds, CancellationToken cancellationToken)
    {
        var ids = questionIds?
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        return await _questionStore.ListByIdsAsync(ids, cancellationToken);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();
}
