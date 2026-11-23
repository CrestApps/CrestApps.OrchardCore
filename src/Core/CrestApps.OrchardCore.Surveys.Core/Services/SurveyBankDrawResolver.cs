using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Copies the question bank questions each pool draws into the pool, so a published version holds them itself.
/// </summary>
public sealed class SurveyBankDrawResolver
{
    /// <summary>
    /// The most candidates one bank draw adds when it names no limit.
    /// </summary>
    public const int DefaultMaxCandidates = 500;

    /// <summary>
    /// The most candidates one bank draw may add.
    /// </summary>
    public const int HardMaxCandidates = 2000;

    /// <summary>
    /// The property that marks a pool member copied from a bank.
    /// </summary>
    public const string FromBankProperty = "fromBank";

    /// <summary>
    /// The property that holds the bank revision a pool member was copied from.
    /// </summary>
    public const string BankRevisionProperty = "bankRevision";

    private readonly BankQuestionStore _bankQuestionStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyBankDrawResolver"/> class.
    /// </summary>
    /// <param name="bankQuestionStore">The bank question store.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyBankDrawResolver(
        BankQuestionStore bankQuestionStore,
        IStringLocalizer<SurveyBankDrawResolver> stringLocalizer)
    {
        _bankQuestionStore = bankQuestionStore;
        S = stringLocalizer;
    }

    /// <summary>
    /// Resolves every bank draw of a definition into pool members.
    /// </summary>
    /// <param name="definition">The definition, changed in place.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The warnings, such as a draw cut to its limit.</returns>
    public async Task<IReadOnlyList<SurveyValidationMessage>> ResolveAsync(SurveyDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var messages = new List<SurveyValidationMessage>();
        var usedIds = SurveyDefinitions.Questions(definition).Select(item => item.Question.Id).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        var usedNames = SurveyDefinitions.Questions(definition).Select(item => item.Question.Name).Concat(definition.Variables.Select(variable => variable.Name)).Where(name => name is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var page in definition.Pages)
        {
            foreach (var pool in page.Pools)
            {
                foreach (var draw in pool.BankDraws.Where(draw => !string.IsNullOrEmpty(draw.BankId)))
                {
                    var limit = Math.Clamp(draw.MaxCandidates ?? DefaultMaxCandidates, 1, HardMaxCandidates);

                    // One more than the limit tells that more match; the questions already in the definition are skipped,
                    // so as many more are read.
                    var candidates = await CandidatesAsync(draw, limit + 1 + usedIds.Count, cancellationToken);
                    var added = 0;

                    foreach (var bankQuestion in candidates)
                    {
                        if (!usedIds.Add(bankQuestion.ItemId))
                        {
                            continue;
                        }

                        if (added == limit)
                        {
                            messages.Add(new SurveyValidationMessage
                            {
                                Code = "bank-draw-capped",
                                Message = S["The pool {0} takes the first {1} matching questions of a question bank; more match.", pool.Title ?? pool.Name ?? string.Empty, limit],
                                PageId = page.Id,
                            });

                            break;
                        }

                        var question = SurveyDefinitionCloner.Clone(bankQuestion.Question) ?? new SurveyQuestion();
                        question.Id = bankQuestion.ItemId;
                        question.PoolId = pool.Id;
                        question.Name = UniqueName(question.Name, usedNames);
                        question.Properties[FromBankProperty] = true;
                        question.Properties[BankRevisionProperty] = bankQuestion.Revision;
                        page.Questions.Add(question);
                        added++;
                    }
                }
            }
        }

        return messages;
    }

    /// <summary>
    /// Removes the pool members a resolution copied from banks, so a definition restored from a version draws again.
    /// </summary>
    /// <param name="definition">The definition, changed in place.</param>
    public static void RemoveResolved(SurveyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var page in definition.Pages)
        {
            page.Questions.RemoveAll(question => question.Properties?[FromBankProperty] is { } value && value.GetValueKind() == System.Text.Json.JsonValueKind.True);
        }
    }

    private async Task<IReadOnlyList<BankQuestion>> CandidatesAsync(BankDraw draw, int take, CancellationToken cancellationToken)
    {
        // The candidates are paged in the database by id; a draw never reads the whole bank.
        var ids = draw.Tags is { Count: > 0 }
            ? await _bankQuestionStore.ListIdsWithTagsAsync(draw.BankId, draw.Tags, cancellationToken)
            : null;

        var questions = await _bankQuestionStore.ListDrawCandidatesAsync(draw.BankId, ids, draw.Difficulty, take, cancellationToken);

        return questions
            .Where(question => !question.Retired && question.BankId == draw.BankId && question.Question is not null)
            .Where(question => draw.Difficulty is null || question.Difficulty == draw.Difficulty)
            .OrderBy(question => question.ItemId, StringComparer.Ordinal)
            .ToList();
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        var baseName = SurveyNames.IsValidVariableCode(name) ? name : "bank_q";
        var candidate = baseName;

        for (var i = 2; !used.Add(candidate); i++)
        {
            candidate = $"{baseName}_{i}";
        }

        return candidate;
    }
}
