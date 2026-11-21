using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Surveys.Drivers;

/// <summary>
/// The editor of the survey part: the questions (one for a choice), anonymous answers, changing answers, the close
/// date in the editor's time zone, and when learners see the totals.
/// </summary>
internal sealed class SurveyPartDisplayDriver : ContentPartDisplayDriver<SurveyPart>
{
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public SurveyPartDisplayDriver(ILocalClock localClock, IStringLocalizer<SurveyPartDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SurveyPart part, BuildPartEditorContext context)
        => Initialize<SurveyPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Questions = part.Questions.Select(question => new SurveyQuestionRow
            {
                Id = question.Id,
                Text = question.Text,
                Type = question.Type,
                Options = string.Join(Environment.NewLine, question.Options ?? []),
                Required = question.Required,
            }).ToList();
            model.Anonymous = part.Anonymous;
            model.AllowChangeAnswer = part.AllowChangeAnswer;
            model.Close = part.CloseUtc is { } close ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(close, DateTimeKind.Utc)))).DateTime : null;
            model.ResultsVisibility = part.ResultsVisibility;
            model.SurveyId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;
            Populate(model, context.TypePartDefinition);
        });

    public override async Task<IDisplayResult> UpdateAsync(SurveyPart part, UpdatePartEditorContext context)
    {
        var model = new SurveyPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var singleQuestion = IsSingleQuestion(context.TypePartDefinition);
        var rows = (model.Questions ?? []).Where(row => !string.IsNullOrWhiteSpace(row?.Text)).ToList();

        if (rows.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Questions), S["Add a question."]);
        }

        if (rows.Count > (singleQuestion ? 1 : SurveysConstants.MaxQuestions))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Questions), singleQuestion ? S["A choice has one question."] : S["A survey can have up to {0} questions.", SurveysConstants.MaxQuestions]);
        }

        var questions = new List<SurveyQuestion>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var type = singleQuestion && row.Type is not (SurveyQuestionType.SingleChoice or SurveyQuestionType.MultipleChoice) ? SurveyQuestionType.SingleChoice : row.Type;
            var options = type is SurveyQuestionType.SingleChoice or SurveyQuestionType.MultipleChoice
                ? (row.Options ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList()
                : [];

            if (type is (SurveyQuestionType.SingleChoice or SurveyQuestionType.MultipleChoice) && options.Count < 2)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Questions), S["The question \"{0}\" needs at least two choices, one a line.", row.Text.Trim()]);
            }

            if (options.Count > SurveysConstants.MaxOptions)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Questions), S["The question \"{0}\" can have up to {1} choices.", row.Text.Trim(), SurveysConstants.MaxOptions]);
            }

            // An id the editor posts back is kept, so answers given before still belong to their question.
            var id = string.IsNullOrWhiteSpace(row.Id) || !ids.Add(row.Id.Trim()) ? IdGenerator.GenerateId() : row.Id.Trim();

            ids.Add(id);
            questions.Add(new SurveyQuestion
            {
                Id = id,
                Text = row.Text.Trim(),
                Type = type,
                Options = options,
                Required = row.Required,
            });
        }

        part.Questions = questions;
        part.Anonymous = model.Anonymous;
        part.AllowChangeAnswer = model.AllowChangeAnswer;
        part.CloseUtc = model.Close is { } close ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(close, DateTimeKind.Unspecified)) : null;
        part.ResultsVisibility = Enum.IsDefined(model.ResultsVisibility) ? model.ResultsVisibility : SurveyResultsVisibility.Never;

        return Edit(part, context);
    }

    private static bool IsSingleQuestion(ContentTypePartDefinition definition)
        => definition?.GetSettings<SurveyPartSettings>()?.SingleQuestion == true;

    private void Populate(SurveyPartViewModel model, ContentTypePartDefinition definition)
    {
        model.SingleQuestion = IsSingleQuestion(definition);

        if (model.Questions.Count == 0)
        {
            model.Questions.Add(new SurveyQuestionRow { Type = SurveyQuestionType.SingleChoice, Required = model.SingleQuestion });
        }

        model.TypeOptions = model.SingleQuestion
            ?
            [
                new SelectListItem(S["One choice"], nameof(SurveyQuestionType.SingleChoice)),
                new SelectListItem(S["Several choices"], nameof(SurveyQuestionType.MultipleChoice)),
            ]
            :
            [
                new SelectListItem(S["One choice"], nameof(SurveyQuestionType.SingleChoice)),
                new SelectListItem(S["Several choices"], nameof(SurveyQuestionType.MultipleChoice)),
                new SelectListItem(S["Rating from 1 to 5"], nameof(SurveyQuestionType.Rating)),
                new SelectListItem(S["Written answer"], nameof(SurveyQuestionType.Text)),
            ];
        model.ResultsVisibilityOptions =
        [
            new SelectListItem(S["Never: only the course staff see them"], nameof(SurveyResultsVisibility.Never)),
            new SelectListItem(S["Once the learner answered"], nameof(SurveyResultsVisibility.AfterAnswer)),
            new SelectListItem(S["Once the survey closed"], nameof(SurveyResultsVisibility.AfterClose)),
            new SelectListItem(S["Always"], nameof(SurveyResultsVisibility.Always)),
        ];
    }
}
