using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the course part: code, summary, enrollment, completion criteria, gradebook categories, prerequisites
/// and staff.
/// </summary>
internal sealed class CoursePartDisplayDriver : ContentPartDisplayDriver<CoursePart>
{
    private readonly CourseEditorOptions _editorOptions;
    private readonly CourseSettingsPolicy _settingsPolicy;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserSearchService _userSearchService;
    private readonly LearningSecretStore _keyStore;

    internal readonly IStringLocalizer S;

    public CoursePartDisplayDriver(
        CourseEditorOptions editorOptions,
        CourseSettingsPolicy settingsPolicy,
        IHttpContextAccessor httpContextAccessor,
        IUserSearchService userSearchService,
        LearningSecretStore keyStore,
        IStringLocalizer<CoursePartDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        _settingsPolicy = settingsPolicy;
        _httpContextAccessor = httpContextAccessor;
        _userSearchService = userSearchService;
        _keyStore = keyStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CoursePart part, BuildPartEditorContext context)
    {
        return Initialize<CoursePartViewModel>(GetEditorShapeType(context), async model =>
        {
            var courseId = part.ContentItem.ContentItemId;

            model.CanChangeEnrollmentSettings = await _settingsPolicy.CanChangeEnrollmentSettingsAsync(_httpContextAccessor.HttpContext?.User, courseId);
            model.Code = part.Code;
            model.Summary = part.Summary;
            model.Level = part.Level;
            model.EstimatedMinutes = part.EstimatedMinutes;
            model.EnrollmentMode = part.EnrollmentMode;
            // A course that was never saved has no key, even when this request wrote one before another part of the
            // form refused the save: the write is thrown away with the item.
            model.HasEnrollmentKey = part.ContentItem.Id != 0 && await _keyStore.HasAsync(LearningSecretKinds.EnrollmentKey, courseId);
            model.AccessDays = part.AccessDays;
            model.PassMark = part.PassMark;
            model.MinimumGrade = ReadDecimal(part, LearningRuleNames.MinimumGrade, "Grade");
            model.MinimumMinutes = (int?)ReadDecimal(part, LearningRuleNames.MinimumTime, "Minutes");
            model.RequireStaffApproval = part.CompletionCriteria.Any(rule => rule.Name == LearningRuleNames.StaffApproval);
            model.FinalItemId = part.CompletionCriteria.FirstOrDefault(rule => rule.Name == LearningRuleNames.ItemPassed)?.Get("ItemId");
            model.PrerequisiteCourseIds = part.Prerequisites
                .Where(prerequisite => prerequisite.Kind == CompletedCoursePrerequisiteEvaluator.CourseKind)
                .Select(prerequisite => prerequisite.Id)
                .ToArray();
            model.InstructorIds = StaffIds(part, LearningConstants.CourseStaffRoles.Instructor);
            model.AssistantIds = StaffIds(part, LearningConstants.CourseStaffRoles.Assistant);
            model.GraderIds = StaffIds(part, LearningConstants.CourseStaffRoles.Grader);
            model.ObserverIds = StaffIds(part, LearningConstants.CourseStaffRoles.Observer);
            model.GradeCategories = (part.GradeCategories ?? [])
                .Select(category => new GradeCategoryRowViewModel { Id = category.Id, Name = category.Name, Weight = category.Weight, DropLowest = category.DropLowest })
                .ToList();
            model.ItemOptions = await _editorOptions.GetItemOptionsAsync(courseId, null, S["No final assessment"], model.FinalItemId);
            model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(courseId, model.PrerequisiteCourseIds);
            model.EnrollmentModeOptions =
            [
                new SelectListItem(S["Closed: staff, pipelines and workflows enroll learners"], nameof(EnrollmentMode.Closed), part.EnrollmentMode == EnrollmentMode.Closed),
                new SelectListItem(S["Open: a signed-in learner enrolls at once"], nameof(EnrollmentMode.Open), part.EnrollmentMode == EnrollmentMode.Open),
                new SelectListItem(S["Enrollment key: a learner enrolls with the key"], nameof(EnrollmentMode.Key), part.EnrollmentMode == EnrollmentMode.Key),
                new SelectListItem(S["Approval: a learner asks and waits for approval"], nameof(EnrollmentMode.Approval), part.EnrollmentMode == EnrollmentMode.Approval),
            ];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(CoursePart part, UpdatePartEditorContext context)
    {
        var model = new CoursePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.PassMark is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.PassMark), S["The pass mark is a percentage from 0 to 100."]);
        }

        if (model.MinimumGrade is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MinimumGrade), S["The minimum grade is a percentage from 0 to 100."]);
        }

        if (model.AccessDays is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AccessDays), S["Access is at least one day, or empty for no end."]);
        }

        // The staff, the enrollment mode and the key decide who gets in and who reads learner records, so only a course
        // manager or an instructor of the published course changes them; for anyone else the stored values stay.
        var canChangeSettings = await _settingsPolicy.CanChangeEnrollmentSettingsAsync(_httpContextAccessor.HttpContext?.User, part.ContentItem.ContentItemId);

        if (!canChangeSettings)
        {
            model.EnrollmentMode = part.EnrollmentMode;
            model.NewEnrollmentKey = null;
            model.ClearEnrollmentKey = false;
            model.InstructorIds = StaffIds(part, LearningConstants.CourseStaffRoles.Instructor);
            model.AssistantIds = StaffIds(part, LearningConstants.CourseStaffRoles.Assistant);
            model.GraderIds = StaffIds(part, LearningConstants.CourseStaffRoles.Grader);
            model.ObserverIds = StaffIds(part, LearningConstants.CourseStaffRoles.Observer);
        }
        else
        {
            var posted = (model.InstructorIds ?? []).Concat(model.AssistantIds ?? []).Concat(model.GraderIds ?? []).Concat(model.ObserverIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var known = posted.Length == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await _userSearchService.GetAsync(posted)).Select(user => user.UserId).ToHashSet(StringComparer.Ordinal);

            if (posted.Any(id => !known.Contains(id)))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.InstructorIds), S["A person chosen for the course staff does not exist."]);
            }
        }

        var keyNeeded = model.EnrollmentMode == EnrollmentMode.Key;
        var hasKey = await _keyStore.HasAsync(LearningSecretKinds.EnrollmentKey, part.ContentItem.ContentItemId) && !model.ClearEnrollmentKey;

        if (keyNeeded && !hasKey && string.IsNullOrWhiteSpace(model.NewEnrollmentKey))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.NewEnrollmentKey), S["A course that needs an enrollment key needs a key."]);
        }

        if (!string.IsNullOrWhiteSpace(model.NewEnrollmentKey) && model.NewEnrollmentKey.Trim().Length < 6)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.NewEnrollmentKey), S["An enrollment key has at least 6 characters."]);
        }

        part.Code = model.Code?.Trim();
        part.Summary = model.Summary?.Trim();
        part.Level = model.Level?.Trim();
        part.EstimatedMinutes = model.EstimatedMinutes is > 0 ? model.EstimatedMinutes : null;
        part.EnrollmentMode = model.EnrollmentMode;
        part.AccessDays = model.AccessDays;
        part.PassMark = model.PassMark;
        part.GradeCategories = ReadGradeCategories(part, model, context);

        // The key is kept in its own record, out of the content item; it changes only when the rest of the form is valid.
        if (context.Updater.ModelState.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(model.NewEnrollmentKey))
            {
                await _keyStore.SetAsync(LearningSecretKinds.EnrollmentKey, part.ContentItem.ContentItemId, model.NewEnrollmentKey);
            }
            else if (model.ClearEnrollmentKey)
            {
                await _keyStore.SetHashAsync(LearningSecretKinds.EnrollmentKey, part.ContentItem.ContentItemId, null);
            }
        }

        var criteria = part.CompletionCriteria
            .Where(rule => rule.Name is not (LearningRuleNames.MinimumGrade or LearningRuleNames.MinimumTime or LearningRuleNames.StaffApproval or LearningRuleNames.ItemPassed))
            .ToList();

        if (model.MinimumGrade is { } grade)
        {
            criteria.Add(Rule(LearningRuleNames.MinimumGrade, "Grade", grade.ToString(CultureInfo.InvariantCulture)));
        }

        if (model.MinimumMinutes is > 0)
        {
            criteria.Add(Rule(LearningRuleNames.MinimumTime, "Minutes", model.MinimumMinutes.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (model.RequireStaffApproval)
        {
            criteria.Add(new LearningRuleSettings { Name = LearningRuleNames.StaffApproval });
        }

        if (!string.IsNullOrEmpty(model.FinalItemId))
        {
            criteria.Add(Rule(LearningRuleNames.ItemPassed, "ItemId", model.FinalItemId));
        }

        part.CompletionCriteria = criteria;
        part.Prerequisites = part.Prerequisites
            .Where(prerequisite => prerequisite.Kind != CompletedCoursePrerequisiteEvaluator.CourseKind)
            .Concat((model.PrerequisiteCourseIds ?? [])
                .Where(id => !string.IsNullOrEmpty(id) && id != part.ContentItem.ContentItemId)
                .Distinct(StringComparer.Ordinal)
                .Select(id => new CoursePrerequisite { Kind = CompletedCoursePrerequisiteEvaluator.CourseKind, Id = id }))
            .ToList();

        part.Staff = part.Staff
            .Where(staff => staff.Role is not (LearningConstants.CourseStaffRoles.Instructor or LearningConstants.CourseStaffRoles.Assistant or LearningConstants.CourseStaffRoles.Grader or LearningConstants.CourseStaffRoles.Observer))
            .Concat(Staff(model.InstructorIds, LearningConstants.CourseStaffRoles.Instructor))
            .Concat(Staff(model.AssistantIds, LearningConstants.CourseStaffRoles.Assistant))
            .Concat(Staff(model.GraderIds, LearningConstants.CourseStaffRoles.Grader))
            .Concat(Staff(model.ObserverIds, LearningConstants.CourseStaffRoles.Observer))
            .ToList();

        return Edit(part, context);
    }

    /// <summary>
    /// Reads the gradebook categories posted by the editor. Rows without a name are left out. A row keeps its id only
    /// when the course already has a category with that id; any other row gets a new id here, so ids never come from
    /// the browser.
    /// </summary>
    private List<GradeCategory> ReadGradeCategories(CoursePart part, CoursePartViewModel model, UpdatePartEditorContext context)
    {
        var existing = (part.GradeCategories ?? [])
            .Where(category => !string.IsNullOrEmpty(category?.Id))
            .Select(category => category.Id)
            .ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var categories = new List<GradeCategory>();

        foreach (var row in model.GradeCategories ?? [])
        {
            if (string.IsNullOrWhiteSpace(row?.Name))
            {
                continue;
            }

            var name = row.Name.Trim();

            if (row.Weight is not > 0)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.GradeCategories), S["The weight of the category '{0}' must be more than zero.", name]);
            }

            if (row.DropLowest is < 0)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.GradeCategories), S["The category '{0}' cannot drop fewer than zero scores.", name]);
            }

            var id = row.Id?.Trim();

            if (string.IsNullOrEmpty(id) || !existing.Contains(id) || !used.Add(id))
            {
                id = IdGenerator.GenerateId();
                used.Add(id);
            }

            categories.Add(new GradeCategory
            {
                Id = id,
                Name = name,
                Weight = row.Weight is > 0 ? row.Weight.Value : 1,
                DropLowest = row.DropLowest is > 0 ? row.DropLowest.Value : 0,
            });
        }

        if (categories.GroupBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase).Any(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.GradeCategories), S["Give each grade category a different name."]);
        }

        return categories;
    }

    private static decimal? ReadDecimal(CoursePart part, string ruleName, string key)
    {
        var value = part.CompletionCriteria.FirstOrDefault(rule => rule.Name == ruleName)?.Get(key);

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static LearningRuleSettings Rule(string name, string key, string value)
    {
        var rule = new LearningRuleSettings { Name = name };

        rule.Values[key] = value;

        return rule;
    }

    private static string[] StaffIds(CoursePart part, string role)
        => part.Staff.Where(staff => staff.Role == role && !string.IsNullOrEmpty(staff.UserId)).Select(staff => staff.UserId).ToArray();

    private static IEnumerable<CourseStaffMember> Staff(IEnumerable<string> userIds, string role)
        => (userIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => new CourseStaffMember { UserId = id, Role = role });
}
