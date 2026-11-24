using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
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

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The editor of the coursework part: the due date in the editor's time zone, the late policy, what the learner hands
/// in, the file rules, resubmission, and the rubric or points the work is graded against.
/// </summary>
internal sealed class CourseworkPartDisplayDriver : ContentPartDisplayDriver<CourseworkPart>
{
    private readonly ILocalClock _localClock;
    private readonly ISession _session;
    private readonly CourseworkOptions _options;

    internal readonly IStringLocalizer S;

    public CourseworkPartDisplayDriver(ILocalClock localClock, ISession session, IOptions<CourseworkOptions> options, IStringLocalizer<CourseworkPartDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _session = session;
        _options = options.Value;
        S = stringLocalizer;
    }

    private int MaxFileSizeMbLimit => Math.Max(_options.MaxFileSizeMb, 1);

    private int MaxFilesLimit => Math.Max(_options.MaxFilesPerSubmission, 1);

    public override IDisplayResult Edit(CourseworkPart part, BuildPartEditorContext context)
        => Initialize<CourseworkPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Due = part.DueUtc is { } due ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(due, DateTimeKind.Utc)))).DateTime : null;
            model.LatePenaltyPercentPerDay = part.LatePenaltyPercentPerDay;
            model.MaxLatePenaltyPercent = part.MaxLatePenaltyPercent;
            model.CloseAfterDays = part.CloseAfterDays;
            model.SubmissionType = part.SubmissionType;
            model.AllowedExtensions = string.Join(", ", part.AllowedExtensions ?? []);
            model.MaxFileSizeMb = part.MaxFileSizeMb;
            model.MaxFiles = part.MaxFiles;
            model.AllowResubmission = part.AllowResubmission;
            model.RubricId = part.RubricId;
            model.MaxPoints = part.MaxPoints;
            model.CourseworkId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;
            model.MaxFilesLimit = MaxFilesLimit;
            model.MaxFileSizeMbLimit = MaxFileSizeMbLimit;

            await PopulateOptionsAsync(model);
        });

    public override async Task<IDisplayResult> UpdateAsync(CourseworkPart part, UpdatePartEditorContext context)
    {
        var model = new CourseworkPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxFileSizeMb < 1 || model.MaxFileSizeMb > MaxFileSizeMbLimit)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxFileSizeMb), S["The largest file must be between 1 and {0} MB.", MaxFileSizeMbLimit]);
        }

        if (model.MaxFiles < 1 || model.MaxFiles > MaxFilesLimit)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxFiles), S["The most files must be between 1 and {0}.", MaxFilesLimit]);
        }

        if (string.IsNullOrEmpty(model.RubricId) && model.MaxPoints <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxPoints), S["Grade the work out of more than zero points, or choose a rubric."]);
        }

        if (!string.IsNullOrEmpty(model.RubricId) && !await IsPublishedRubricAsync(model.RubricId))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.RubricId), S["Choose a published rubric."]);
        }

        if (model.LatePenaltyPercentPerDay is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.LatePenaltyPercentPerDay), S["The late penalty a day is a percentage from 0 to 100."]);
        }

        if (model.MaxLatePenaltyPercent is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxLatePenaltyPercent), S["The largest late penalty is a percentage from 0 to 100."]);
        }

        if (model.CloseAfterDays is < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CloseAfterDays), S["Close the coursework zero or more days after the due date, or leave it empty."]);
        }

        part.DueUtc = model.Due is { } due ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(due, DateTimeKind.Unspecified)) : null;
        part.LatePenaltyPercentPerDay = Math.Clamp(model.LatePenaltyPercentPerDay, 0, 100);
        part.MaxLatePenaltyPercent = Math.Clamp(model.MaxLatePenaltyPercent, 0, 100);
        part.CloseAfterDays = model.CloseAfterDays is >= 0 ? model.CloseAfterDays : null;
        part.SubmissionType = model.SubmissionType;
        part.AllowedExtensions = ParseExtensions(model.AllowedExtensions);
        part.MaxFileSizeMb = Math.Clamp(model.MaxFileSizeMb, 1, MaxFileSizeMbLimit);
        part.MaxFiles = Math.Clamp(model.MaxFiles, 1, MaxFilesLimit);
        part.AllowResubmission = model.AllowResubmission;
        part.RubricId = string.IsNullOrEmpty(model.RubricId) ? null : model.RubricId;
        part.MaxPoints = model.MaxPoints;

        return Edit(part, context);
    }

    /// <summary>
    /// Reads a list of extensions such as <c>pdf, .DOCX</c> into <c>.pdf</c> and <c>.docx</c>.
    /// </summary>
    /// <param name="text">The extensions, separated by commas, semicolons or spaces.</param>
    /// <returns>The extensions, each once.</returns>
    internal static List<string> ParseExtensions(string text)
        => (text ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => "." + extension.TrimStart('.').ToLower(CultureInfo.InvariantCulture))
            .Where(extension => extension.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private async Task PopulateOptionsAsync(CourseworkPartViewModel model)
    {
        var rubrics = await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == CourseworkConstants.ContentTypes.Rubric && index.Published)
            .OrderBy(index => index.DisplayText)
            .Take(500)
            .ListAsync();

        model.RubricOptions = rubrics
            .Select(rubric => new SelectListItem(rubric.DisplayText, rubric.ContentItemId, rubric.ContentItemId == model.RubricId))
            .ToList();
        model.SubmissionTypeOptions =
        [
            new SelectListItem(S["Text or files, or both"], nameof(CourseworkSubmissionType.TextAndFiles), model.SubmissionType == CourseworkSubmissionType.TextAndFiles),
            new SelectListItem(S["Text only"], nameof(CourseworkSubmissionType.Text), model.SubmissionType == CourseworkSubmissionType.Text),
            new SelectListItem(S["Files only"], nameof(CourseworkSubmissionType.Files), model.SubmissionType == CourseworkSubmissionType.Files),
            new SelectListItem(S["Nothing: a grader signs it off"], nameof(CourseworkSubmissionType.SignOff), model.SubmissionType == CourseworkSubmissionType.SignOff),
        ];
        model.MaxFilesLimit = MaxFilesLimit;
        model.MaxFileSizeMbLimit = MaxFileSizeMbLimit;
    }

    private async Task<bool> IsPublishedRubricAsync(string rubricId)
        => await _session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == rubricId && index.ContentType == CourseworkConstants.ContentTypes.Rubric && index.Published)
            .CountAsync() > 0;
}
