using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Lists.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the course item part: how the item is completed, whether it is graded and in which gradebook category
/// of its course, whether it is a free preview and when it opens. The kind comes from the content type and never
/// changes on an item.
/// </summary>
internal sealed class CourseItemPartDisplayDriver : ContentPartDisplayDriver<CourseItemPart>
{
    private readonly CourseEditorOptions _editorOptions;
    private readonly CourseItemKindOptions _kindOptions;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public CourseItemPartDisplayDriver(
        CourseEditorOptions editorOptions,
        IOptions<CourseItemKindOptions> kindOptions,
        ILocalClock localClock,
        IStringLocalizer<CourseItemPartDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        _kindOptions = kindOptions.Value;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CourseItemPart part, BuildPartEditorContext context)
    {
        return Initialize<CourseItemPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<CourseItemPartSettings>();
            var kind = _kindOptions.Find(settings.Kind ?? part.Kind);
            var mode = string.IsNullOrEmpty(part.CompletionMode) ? settings.DefaultCompletionMode ?? kind?.DefaultCompletionMode : part.CompletionMode;
            var after = part.ReleaseRules.FirstOrDefault(rule => rule.Name == LearningRuleNames.ItemCompleted);
            var from = part.ReleaseRules.FirstOrDefault(rule => rule.Name == LearningRuleNames.Date)?.Get("FromUtc");
            var days = part.ReleaseRules.FirstOrDefault(rule => rule.Name == LearningRuleNames.DaysAfterEnrollment)?.Get("Days");

            model.KindDisplayName = kind?.DisplayName?.Value ?? settings.Kind ?? part.Kind;
            model.ProducesGrade = kind?.ProducesGrade ?? false;
            model.CompletionMode = mode;
            model.MinimumMinutes = part.MinimumMinutes;
            model.EstimatedMinutes = part.EstimatedMinutes;
            model.Gradable = part.Gradable;
            model.Weight = part.Weight;
            model.PassMark = part.PassMark;
            model.FreePreview = part.FreePreview;
            model.AllowSelfReported = part.AllowSelfReported;
            model.AfterItemId = after?.Get("ItemId");
            model.AfterMinimumScore = decimal.TryParse(after?.Get("MinimumScore"), NumberStyles.Number, CultureInfo.InvariantCulture, out var score) ? score : null;
            model.OpensDaysAfterEnrollment = int.TryParse(days, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opensDays) ? opensDays : null;

            if (from is not null && DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fromUtc))
            {
                model.OpensFrom = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc))).DateTime;
            }

            foreach (var candidate in kind?.CompletionModes ?? [mode ?? CompletionModes.Viewed])
            {
                model.CompletionModeOptions.Add(new SelectListItem(ModeText(candidate), candidate, candidate == mode));
            }

            var courseId = part.ContentItem.TryGet<ContainedPart>(out var contained) ? contained.ListContentItemId : null;

            model.ItemOptions = await _editorOptions.GetItemOptionsAsync(courseId, part.ContentItem.ContentItemId, S["Opens with the course"], model.AfterItemId);

            var categories = await _editorOptions.GetGradeCategoriesAsync(courseId);

            model.GradeCategoryId = part.GradeCategoryId;

            if (categories.Count > 0)
            {
                model.GradeCategoryOptions.Add(new SelectListItem(S["No category"], string.Empty, string.IsNullOrEmpty(part.GradeCategoryId)));

                foreach (var category in categories)
                {
                    model.GradeCategoryOptions.Add(new SelectListItem(category.Name, category.Id, category.Id == part.GradeCategoryId));
                }
            }
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(CourseItemPart part, UpdatePartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<CourseItemPartSettings>();
        var kind = _kindOptions.Find(settings.Kind ?? part.Kind);
        var model = new CourseItemPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (kind is not null && !kind.Supports(model.CompletionMode))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CompletionMode), S["Choose how the item is completed."]);
        }
        else if (kind is not null && !kind.Allows(model.CompletionMode, model.Gradable))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CompletionMode), S["A gradable item of this kind completes when it is graded, so its grade reaches the course grade."]);
        }

        if (model.PassMark is < 0 or > 100 || model.AfterMinimumScore is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.PassMark), S["Scores are percentages from 0 to 100."]);
        }

        if (model.CompletionMode == CompletionModes.MinimumTime && model.MinimumMinutes is not > 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MinimumMinutes), S["Give the minimum time, in minutes."]);
        }

        if (model.Weight < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Weight), S["The weight cannot be negative."]);
        }

        part.Kind = settings.Kind ?? part.Kind;
        part.CompletionMode = model.CompletionMode;
        part.MinimumMinutes = model.MinimumMinutes is > 0 ? model.MinimumMinutes : null;
        part.EstimatedMinutes = model.EstimatedMinutes is > 0 ? model.EstimatedMinutes : null;
        part.Gradable = model.Gradable;
        part.Weight = model.Weight;
        part.PassMark = model.PassMark;
        part.FreePreview = model.FreePreview;

        // Only a category of the item's course is kept; the course is the list that contains the item.
        var courseId = part.ContentItem.TryGet<ContainedPart>(out var contained) ? contained.ListContentItemId : null;
        var categories = await _editorOptions.GetGradeCategoriesAsync(courseId);

        part.GradeCategoryId = categories.Any(category => category.Id == model.GradeCategoryId) ? model.GradeCategoryId : null;
        part.AllowSelfReported = model.AllowSelfReported;

        var rules = part.ReleaseRules
            .Where(rule => rule.Name is not (LearningRuleNames.ItemCompleted or LearningRuleNames.Date or LearningRuleNames.DaysAfterEnrollment))
            .ToList();

        if (!string.IsNullOrEmpty(model.AfterItemId) && model.AfterItemId != part.ContentItem.ContentItemId)
        {
            var rule = new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted };

            rule.Values["ItemId"] = model.AfterItemId;

            if (model.AfterMinimumScore is { } minimum)
            {
                rule.Values["MinimumScore"] = minimum.ToString(CultureInfo.InvariantCulture);
            }

            rules.Add(rule);
        }

        if (model.OpensFrom is { } opensFrom)
        {
            var fromUtc = await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(opensFrom, DateTimeKind.Unspecified));
            var rule = new LearningRuleSettings { Name = LearningRuleNames.Date };

            rule.Values["FromUtc"] = fromUtc.ToString("O", CultureInfo.InvariantCulture);
            rules.Add(rule);
        }

        if (model.OpensDaysAfterEnrollment is > 0)
        {
            var rule = new LearningRuleSettings { Name = LearningRuleNames.DaysAfterEnrollment };

            rule.Values["Days"] = model.OpensDaysAfterEnrollment.Value.ToString(CultureInfo.InvariantCulture);
            rules.Add(rule);
        }

        part.ReleaseRules = rules;

        return Edit(part, context);
    }

    private string ModeText(string mode)
        => mode switch
        {
            CompletionModes.Viewed => S["When the learner opens it"],
            CompletionModes.MarkedDone => S["When the learner marks it done"],
            CompletionModes.MinimumTime => S["After a minimum time on it"],
            CompletionModes.Watched => S["When the learner has watched enough of the video"],
            CompletionModes.Acknowledged => S["When the learner confirms the statement"],
            CompletionModes.Submitted => S["When the learner hands in work"],
            CompletionModes.Graded => S["When the work is graded"],
            CompletionModes.Passed => S["When the learner passes"],
            CompletionModes.Attended => S["When the learner attends"],
            CompletionModes.PackageReported => S["When the package reports it complete"],
            _ => mode,
        };
}
