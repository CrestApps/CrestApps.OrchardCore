using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
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
    private const int MaxOptions = 500;

    private readonly global::YesSql.ISession _session;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly ILocalClock _localClock;
    private readonly LearningSecretStore _secretStore;

    internal readonly IStringLocalizer S;

    public QuizPartDisplayDriver(
        global::YesSql.ISession session,
        IOptions<QuestionTypeOptions> questionTypes,
        ILocalClock localClock,
        LearningSecretStore secretStore,
        IStringLocalizer<QuizPartDisplayDriver> stringLocalizer)
    {
        _session = session;
        _questionTypes = questionTypes.Value;
        _localClock = localClock;
        _secretStore = secretStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(QuizPart part, BuildPartEditorContext context)
        => Initialize<QuizPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.QuestionIds = [.. part.QuestionIds];
            model.DrawRules = part.DrawRules.Select(rule => new DrawRuleRowViewModel { BankId = rule.BankId, Tag = rule.Tag, Difficulty = rule.Difficulty, Count = rule.Count }).ToList();

            while (model.DrawRules.Count < part.DrawRules.Count + 2)
            {
                model.DrawRules.Add(new DrawRuleRowViewModel());
            }

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
            model.ReviewPolicy = part.ReviewPolicy;

            var questionTypes = _questionTypes.Types.Select(type => type.ContentType).ToArray();
            var questions = await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(questionTypes))
                .OrderBy(index => index.DisplayText)
                .Take(MaxOptions)
                .ListAsync();
            var banks = await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank)
                .OrderBy(index => index.DisplayText)
                .ListAsync();

            model.QuestionOptions = questions.Select(question => new SelectListItem(question.DisplayText, question.ContentItemId, part.QuestionIds.Contains(question.ContentItemId))).ToList();
            model.BankOptions = banks.Select(bank => new SelectListItem(bank.DisplayText, bank.ContentItemId)).ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(QuizPart part, UpdatePartEditorContext context)
    {
        var model = new QuizPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.QuestionIds = (model.QuestionIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        part.DrawRules = model.DrawRules.Where(rule => !string.IsNullOrEmpty(rule.BankId) && rule.Count > 0)
            .Select(rule => new QuizDrawRule { BankId = rule.BankId, Tag = string.IsNullOrWhiteSpace(rule.Tag) ? null : rule.Tag.Trim(), Difficulty = rule.Difficulty is >= 1 and <= 5 ? rule.Difficulty : null, Count = rule.Count })
            .ToList();
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
        part.ReviewPolicy = model.ReviewPolicy;

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
}
