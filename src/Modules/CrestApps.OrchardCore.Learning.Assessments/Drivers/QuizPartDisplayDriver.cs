using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class QuizPartDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly global::YesSql.ISession _session;
    private readonly ILocalClock _localClock;
    private readonly LearningSecretStore _secretStore;
    private readonly IAssessmentContentSource _contentSource;
    private readonly QuestionTypeOptions _questionTypes;

    internal readonly IStringLocalizer S;

    public QuizPartDisplayDriver(
        global::YesSql.ISession session,
        ILocalClock localClock,
        LearningSecretStore secretStore,
        IAssessmentContentSource contentSource,
        IOptions<QuestionTypeOptions> questionTypes,
        IStringLocalizer<QuizPartDisplayDriver> stringLocalizer)
    {
        _session = session;
        _localClock = localClock;
        _secretStore = secretStore;
        _contentSource = contentSource;
        _questionTypes = questionTypes.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(QuizPart part, BuildPartEditorContext context)
        => Initialize<QuizPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.QuestionIds = [.. part.QuestionIds];
            model.DrawRules = part.DrawRules.Select(rule => new DrawRuleRowViewModel { BankId = rule.BankId, Tag = rule.Tag, Difficulty = rule.Difficulty, Count = rule.Count }).ToList();

            model.ShuffleQuestions = part.ShuffleQuestions;
            model.TimeLimitMinutes = part.TimeLimitMinutes;
            model.MaxAttempts = part.MaxAttempts;
            model.DelayMinutes = part.DelayMinutes;
            model.PassMark = part.PassMark;
            model.GradingMethod = part.GradingMethod;
            model.Navigation = part.Navigation;
            model.Opens = part.OpensUtc is { } opens ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(opens, DateTimeKind.Utc)))).DateTime : null;
            model.Closes = part.ClosesUtc is { } closes ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(closes, DateTimeKind.Utc)))).DateTime : null;
            model.HasAccessCode = await _secretStore.HasAsync(AssessmentsConstants.QuizAccessCodeSecret, part.ContentItem.ContentItemId);
            model.HonorStatement = part.HonorStatement;
            model.ReviewMode = part.ReviewOptions is null ? part.ReviewPolicy.ToString() : QuizPartViewModel.CustomReview;
            var options = part.ReviewOptions ?? QuizReviewOptions.FromPolicy(part.ReviewPolicy);
            model.ReviewMoments = Enum.GetValues<ReviewMoment>().Select(moment =>
            {
                var shows = options.For(moment);

                return new ReviewMomentViewModel
                {
                    Moment = moment,
                    Score = shows.HasFlag(ReviewShows.Score),
                    RightAnswers = shows.HasFlag(ReviewShows.RightAnswers),
                    Feedback = shows.HasFlag(ReviewShows.Feedback),
                    GeneralFeedback = shows.HasFlag(ReviewShows.GeneralFeedback),
                };
            }).ToList();
            model.QuestionsPerPage = part.QuestionsPerPage;
            model.FeedbackMode = part.FeedbackMode;

            // The fixed questions are searched on the server; only the chosen ones are rendered, in the quiz's order.
            var chosen = part.QuestionIds.Count == 0
                ? []
                : (await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(part.QuestionIds)).ListAsync())
                    .GroupBy(question => question.ContentItemId)
                    .ToDictionary(group => group.Key, group => group.First().DisplayText, StringComparer.Ordinal);
            var banks = await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank)
                .OrderBy(index => index.DisplayText)
                .ListAsync();

            model.InitialQuestionsJson = JsonSerializer.Serialize(part.QuestionIds
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => new { value = id, text = chosen.TryGetValue(id, out var title) && !string.IsNullOrEmpty(title) ? title : id }));
            model.BankOptions = banks.Select(bank => new SelectListItem(bank.DisplayText, bank.ContentItemId)).ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(QuizPart part, UpdatePartEditorContext context)
    {
        var model = new QuizPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.QuestionIds = (model.QuestionIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        part.DrawRules = await ReadDrawRulesAsync(model.DrawRules ?? [], context);
        part.ShuffleQuestions = model.ShuffleQuestions;
        part.TimeLimitMinutes = model.TimeLimitMinutes is > 0 ? model.TimeLimitMinutes : null;
        part.MaxAttempts = model.MaxAttempts is > 0 ? model.MaxAttempts : null;
        part.DelayMinutes = Math.Max(model.DelayMinutes, 0);
        part.PassMark = model.PassMark is { } passMark ? Math.Clamp(passMark, 0, 100) : null;
        part.GradingMethod = model.GradingMethod;
        part.Navigation = model.Navigation;
        part.OpensUtc = model.Opens is { } opens ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(opens, DateTimeKind.Unspecified)) : null;
        part.ClosesUtc = model.Closes is { } closes ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(closes, DateTimeKind.Unspecified)) : null;
        part.HonorStatement = string.IsNullOrWhiteSpace(model.HonorStatement) ? null : model.HonorStatement.Trim();
        part.QuestionsPerPage = Math.Clamp(model.QuestionsPerPage, 0, 100);
        part.FeedbackMode = model.FeedbackMode;

        if (string.Equals(model.ReviewMode, QuizPartViewModel.CustomReview, StringComparison.Ordinal))
        {
            ReviewShows Shows(ReviewMoment moment)
            {
                var row = model.ReviewMoments?.FirstOrDefault(entry => entry?.Moment == moment);

                return row is null
                    ? ReviewShows.None
                    : (row.Score ? ReviewShows.Score : ReviewShows.None)
                        | (row.RightAnswers ? ReviewShows.RightAnswers : ReviewShows.None)
                        | (row.Feedback ? ReviewShows.Feedback : ReviewShows.None)
                        | (row.GeneralFeedback ? ReviewShows.GeneralFeedback : ReviewShows.None);
            }

            part.ReviewOptions = new QuizReviewOptions
            {
                During = Shows(ReviewMoment.During),
                ImmediatelyAfter = Shows(ReviewMoment.ImmediatelyAfter),
                LaterWhileOpen = Shows(ReviewMoment.LaterWhileOpen),
                AfterClose = Shows(ReviewMoment.AfterClose),
            };
        }
        else
        {
            part.ReviewPolicy = Enum.TryParse<ReviewPolicy>(model.ReviewMode, out var policy) ? policy : ReviewPolicy.AfterGraded;
            part.ReviewOptions = null;
        }

        // The code is kept in its own record, out of the content item; it changes only when the rest of the form is valid.
        if (context.Updater.ModelState.IsValid)
        {
            if (model.ClearAccessCode)
            {
                await _secretStore.SetHashAsync(AssessmentsConstants.QuizAccessCodeSecret, part.ContentItem.ContentItemId, null);
            }
            else if (!string.IsNullOrWhiteSpace(model.NewAccessCode))
            {
                await _secretStore.SetAsync(AssessmentsConstants.QuizAccessCodeSecret, part.ContentItem.ContentItemId, model.NewAccessCode);
            }
        }

        if (part.QuestionIds.Count == 0 && part.DrawRules.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.QuestionIds), S["Choose questions or add a rule that draws them from a bank."]);
        }

        if (part.OpensUtc is not null && part.ClosesUtc is not null && part.ClosesUtc <= part.OpensUtc)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Closes), S["The quiz must close after it opens."]);
        }

        return Edit(part, context);
    }

    // The rules as stored, each checked against what its bank can supply: an attempt draws only the bank's published
    // questions that match the rule's tag and difficulty, so a rule asking for more could never be met.
    private async Task<List<QuizDrawRule>> ReadDrawRulesAsync(IList<DrawRuleRowViewModel> rows, UpdatePartEditorContext context)
    {
        var rules = new List<QuizDrawRule>();
        var key = Prefix + "." + nameof(QuizPartViewModel.DrawRules);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (row is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(row.BankId))
            {
                context.Updater.ModelState.AddModelError(key, S["Choose the question bank of rule {0}, or remove the rule.", i + 1]);

                continue;
            }

            if (row.Count < 1)
            {
                context.Updater.ModelState.AddModelError(key, S["Rule {0} must draw at least one question.", i + 1]);

                continue;
            }

            var rule = new QuizDrawRule
            {
                BankId = row.BankId,
                Tag = string.IsNullOrWhiteSpace(row.Tag) ? null : row.Tag.Trim(),
                Difficulty = row.Difficulty is >= 1 and <= 5 ? row.Difficulty : null,
                Count = row.Count,
            };

            var available = (await _contentSource.GetBankQuestionsAsync(rule.BankId))
                .Count(question => _questionTypes.IsQuestion(question.ContentType) && QuizDrawRuleMatcher.Matches(question.Get<QuestionPart>(nameof(QuestionPart)), rule));

            if (rule.Count > available)
            {
                var bank = await _contentSource.GetItemAsync(rule.BankId);

                context.Updater.ModelState.AddModelError(key, S["Rule {0} draws {1} questions, but {2} has only {3} that match.", i + 1, rule.Count, bank?.DisplayText ?? rule.BankId, available]);
            }

            rules.Add(rule);
        }

        return rules;
    }
}
