using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The editor of the coursework part: the due date in the editor's time zone, what the learner hands in, the file
/// rules, resubmission, and the rubric or points the work is graded against.
/// </summary>
internal sealed class CourseworkPartDisplayDriver : ContentPartDisplayDriver<CourseworkPart>
{
    internal const int MaxFileSizeMbLimit = 50;
    internal const int MaxFilesLimit = 10;

    private readonly ILocalClock _localClock;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public CourseworkPartDisplayDriver(ILocalClock localClock, ISession session, IStringLocalizer<CourseworkPartDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _session = session;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CourseworkPart part, BuildPartEditorContext context)
        => Initialize<CourseworkPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Due = part.DueUtc is { } due ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(due, DateTimeKind.Utc)))).DateTime : null;
            model.SubmissionType = part.SubmissionType;
            model.AllowedExtensions = string.Join(", ", part.AllowedExtensions ?? []);
            model.MaxFileSizeMb = part.MaxFileSizeMb;
            model.MaxFiles = part.MaxFiles;
            model.AllowResubmission = part.AllowResubmission;
            model.RubricId = part.RubricId;
            model.MaxPoints = part.MaxPoints;
            model.CourseworkId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;

            await PopulateOptionsAsync(model);
        });

    public override async Task<IDisplayResult> UpdateAsync(CourseworkPart part, UpdatePartEditorContext context)
    {
        var model = new CourseworkPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxFileSizeMb is < 1 or > MaxFileSizeMbLimit)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxFileSizeMb), S["The largest file must be between 1 and {0} MB.", MaxFileSizeMbLimit]);
        }

        if (model.MaxFiles is < 1 or > MaxFilesLimit)
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

        part.DueUtc = model.Due is { } due ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(due, DateTimeKind.Unspecified)) : null;
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
        ];
    }

    private async Task<bool> IsPublishedRubricAsync(string rubricId)
        => await _session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == rubricId && index.ContentType == CourseworkConstants.ContentTypes.Rubric && index.Published)
            .CountAsync() > 0;
}
