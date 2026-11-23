using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports question banks with their questions. A bank is matched by id, then by name; a question by id, then by its
/// import source identifier within the bank.
/// </summary>
internal sealed class SurveyQuestionBanksRecipeStep : NamedRecipeStepHandler
{
    private readonly QuestionBankStore _bankStore;
    private readonly BankQuestionStore _questionStore;
    private readonly ISurveyUserLookup _userLookup;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuestionBanksRecipeStep"/> class.
    /// </summary>
    /// <param name="bankStore">The bank store.</param>
    /// <param name="questionStore">The bank question store.</param>
    /// <param name="userLookup">The user lookup, to find the owner by user name.</param>
    /// <param name="clock">The clock.</param>
    public SurveyQuestionBanksRecipeStep(
        QuestionBankStore bankStore,
        BankQuestionStore questionStore,
        ISurveyUserLookup userLookup,
        IClock clock)
        : base(SurveysRecipeSteps.QuestionBanks)
    {
        _bankStore = bankStore;
        _questionStore = questionStore;
        _userLookup = userLookup;
        _clock = clock;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.QuestionBanksSection))
        {
            var name = SurveysRecipeJson.String(token, nameof(QuestionBank.Name));

            if (string.IsNullOrEmpty(name))
            {
                context.Errors.Add("A question bank in the SurveyQuestionBanks step has no Name and was skipped.");

                continue;
            }

            var id = SurveysRecipeJson.String(token, nameof(QuestionBank.ItemId));
            var bank = (string.IsNullOrEmpty(id) ? null : await _bankStore.FindByIdAsync(id)) ?? await _bankStore.FindByNameAsync(name);
            var now = _clock.UtcNow;

            if (bank is null)
            {
                var imported = SurveysRecipeJson.Read<QuestionBank>(token, "Questions", SurveysRecipeJson.OwnerUserName);

                imported.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
                imported.Name = name;
                imported.CreatedUtc = now;
                imported.ModifiedUtc = now;

                await AssignOwnerAsync(imported, token);
                await _bankStore.CreateAsync(imported);
                bank = imported;
            }
            else
            {
                SurveysRecipeJson.Populate(bank, token, "Questions", SurveysRecipeJson.OwnerUserName);
                bank.Name = name;
                bank.ModifiedUtc = now;
                await AssignOwnerAsync(bank, token);
                await _bankStore.UpdateAsync(bank);
            }

            foreach (var node in SurveysRecipeJson.Array(token, "Questions"))
            {
                var questionId = SurveysRecipeJson.String(node, nameof(BankQuestion.ItemId));
                var source = SurveysRecipeJson.String(node, nameof(BankQuestion.SourceIdentifier));
                var existing = (string.IsNullOrEmpty(questionId) ? null : await _questionStore.FindByIdAsync(questionId))
                    ?? await _questionStore.FindBySourceAsync(bank.ItemId, source);
                if (existing is null)
                {
                    var question = SurveysRecipeJson.Read<BankQuestion>(node, nameof(BankQuestion.BankId));

                    question.BankId = bank.ItemId;
                    question.Question ??= new SurveyQuestion();
                    question.ModifiedUtc = now;
                    question.ItemId = !string.IsNullOrEmpty(questionId) && UniqueId.IsValid(questionId) ? questionId : IdGenerator.GenerateId();
                    question.CreatedUtc = now;
                    question.Revision = Math.Max(question.Revision, 1);

                    // The bank question's id is the question id in every version that draws it.
                    question.Question.Id = question.ItemId;
                    await _questionStore.CreateAsync(question);

                    continue;
                }

                var previousRevision = existing.Revision;

                SurveysRecipeJson.Populate(existing, node, nameof(BankQuestion.BankId), nameof(BankQuestion.Revision));
                existing.BankId = bank.ItemId;
                existing.Question ??= new SurveyQuestion();
                existing.Question.Id = existing.ItemId;
                existing.ModifiedUtc = now;
                existing.Revision = Math.Max(SurveysRecipeJson.Int(node, nameof(BankQuestion.Revision)) ?? 0, previousRevision + 1);
                await _questionStore.UpdateAsync(existing);
            }
        }
    }

    // The owner comes by user name, as user ids differ between sites; a bank that has an owner on this site keeps it.
    private async Task AssignOwnerAsync(QuestionBank bank, System.Text.Json.Nodes.JsonObject token)
    {
        if (string.IsNullOrEmpty(bank.OwnerId)
            && SurveysRecipeJson.String(token, SurveysRecipeJson.OwnerUserName) is { Length: > 0 } ownerName
            && await _userLookup.FindUserIdAsync(ownerName) is { Length: > 0 } ownerId)
        {
            bank.OwnerId = ownerId;
            bank.Author = ownerName;
        }
    }
}
