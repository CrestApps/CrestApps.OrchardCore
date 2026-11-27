using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the course part: code, summary, dates, enrollment, completion criteria, gradebook categories,
/// prerequisites and staff. The dates are entered in the person's time zone and kept in UTC.
/// </summary>
internal sealed class CoursePartDisplayDriver : ContentPartDisplayDriver<CoursePart>
{
    private readonly CourseEditorOptions _editorOptions;
    private readonly CourseSettingsPolicy _settingsPolicy;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserSearchService _userSearchService;
    private readonly LearningSecretStore _keyStore;
    private readonly ILocalClock _localClock;
    private readonly ICourseStructureService _courseStructure;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public CoursePartDisplayDriver(
        CourseEditorOptions editorOptions,
        CourseSettingsPolicy settingsPolicy,
        IHttpContextAccessor httpContextAccessor,
        IUserSearchService userSearchService,
        LearningSecretStore keyStore,
        ILocalClock localClock,
        ICourseStructureService courseStructure,
        IServiceProvider serviceProvider,
        IStringLocalizer<CoursePartDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        _settingsPolicy = settingsPolicy;
        _httpContextAccessor = httpContextAccessor;
        _userSearchService = userSearchService;
        _keyStore = keyStore;
        _localClock = localClock;
        _courseStructure = courseStructure;
        _serviceProvider = serviceProvider;
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
            model.Starts = await ToLocalAsync(part.StartsUtc);
            model.Ends = await ToLocalAsync(part.EndsUtc);
            model.EnrollmentMode = part.EnrollmentMode;
            // A course that was never saved has no key, even when this request wrote one before another part of the
            // form refused the save: the write is thrown away with the item.
            model.HasEnrollmentKey = part.ContentItem.Id != 0 && await _keyStore.HasAsync(LearningSecretKinds.EnrollmentKey, courseId);
            model.AccessDays = part.AccessDays;
            model.PassMark = part.PassMark;
            model.MinimumGrade = ReadDecimal(part, LearningRuleNames.MinimumGrade, "Grade");
            model.MinimumMinutes = (int?)ReadDecimal(part, LearningRuleNames.MinimumTime, "Minutes");
            model.NoGradeSource = (model.MinimumGrade is not null || model.PassMark is not null) && part.ContentItem.Id != 0 && !await HasGradeSourceAsync(part);
            model.RequireStaffApproval = part.CompletionCriteria.Any(rule => rule.Name == LearningRuleNames.StaffApproval);
            model.ApprovalRole = CompletionApprovers.RoleOf(part);
            model.NeverCompletes = part.NeverCompletes;
            model.ItemsInOrder = part.ItemsInOrder;
            model.ApprovalRoleOptions =
            [
                new SelectListItem(S["Anyone who manages the enrollments"], string.Empty, string.IsNullOrEmpty(model.ApprovalRole)),
                new SelectListItem(S["Instructors of the course"], LearningConstants.CourseStaffRoles.Instructor, model.ApprovalRole == LearningConstants.CourseStaffRoles.Instructor),
                new SelectListItem(S["Assistants of the course"], LearningConstants.CourseStaffRoles.Assistant, model.ApprovalRole == LearningConstants.CourseStaffRoles.Assistant),
                new SelectListItem(S["Graders of the course"], LearningConstants.CourseStaffRoles.Grader, model.ApprovalRole == LearningConstants.CourseStaffRoles.Grader),
                new SelectListItem(S["Observers of the course"], LearningConstants.CourseStaffRoles.Observer, model.ApprovalRole == LearningConstants.CourseStaffRoles.Observer),
            ];

            // A role no course staff role matches (from a Moodle import) stays chosen, so saving the course keeps it.
            if (!string.IsNullOrEmpty(model.ApprovalRole) && !model.ApprovalRoleOptions.Any(option => option.Value == model.ApprovalRole))
            {
                model.ApprovalRoleOptions.Add(new SelectListItem(S["Course staff in the role {0} (no course staff role has this name)", model.ApprovalRole], model.ApprovalRole, true));
            }
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
            model.PrerequisiteCoursesJson = await _editorOptions.GetChosenCoursesJsonAsync(model.PrerequisiteCourseIds);
            model.CourseId = courseId;
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

        var startsUtc = model.Starts is { } starts ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(starts, DateTimeKind.Unspecified)) : (DateTime?)null;
        var endsUtc = model.Ends is { } ends ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(ends, DateTimeKind.Unspecified)) : (DateTime?)null;

        if (startsUtc is not null && endsUtc is not null && endsUtc <= startsUtc)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Ends), S["The course must end after it starts."]);
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
        part.StartsUtc = startsUtc is { } startUtc ? DateTime.SpecifyKind(startUtc, DateTimeKind.Utc) : null;
        part.EndsUtc = endsUtc is { } endUtc ? DateTime.SpecifyKind(endUtc, DateTimeKind.Utc) : null;
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
            var approval = new LearningRuleSettings { Name = LearningRuleNames.StaffApproval };

            if (!string.IsNullOrWhiteSpace(model.ApprovalRole))
            {
                approval.Values["Role"] = model.ApprovalRole.Trim();
            }

            criteria.Add(approval);
        }

        part.NeverCompletes = model.NeverCompletes;
        part.ItemsInOrder = model.ItemsInOrder;

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

    // Whether anything in the course can give a score toward the course grade, by the one rule the grade itself follows
    // (CourseGradeCalculator.HasNoGradeSource: gradable items with a weight, in a gradebook category that weighs more than
    // 0), with every item whose survey is anonymous taken as completion-only, since an anonymous survey records the
    // learner's completion only (R10-Mi4). Without one, every learner's course grade is empty, so the minimum course grade
    // is met without a check and the pass mark decides nothing (see MinimumGradeCriterion).
    private async Task<bool> HasGradeSourceAsync(CoursePart part)
    {
        var outline = await _courseStructure.GetOutlineAsync(part.ContentItem);

        if (outline is null)
        {
            return false;
        }

        var completionOnly = new EnrollmentProgress();

        if (_serviceProvider.GetService<ISurveyManager>() is { } surveys)
        {
            var owners = outline.Entries
                .Where(entry => entry.Part?.Gradable == true && !string.IsNullOrEmpty(entry.Item?.ContentItemId))
                .Select(entry => entry.Item.ContentItemId)
                .Distinct(StringComparer.Ordinal)
                .Select(LearningSurveyContexts.Owner)
                .ToList();

            if (owners.Count > 0)
            {
                foreach (var survey in await surveys.ListByOwnersAsync(owners))
                {
                    if (survey?.Access?.Anonymity == AnonymityMode.Anonymous && survey.Owner is { } owner)
                    {
                        completionOnly.Items.Add(new CourseItemProgress { ContentItemId = owner.Id, CompletionOnly = true });
                    }
                }
            }
        }

        return !CourseGradeCalculator.HasNoGradeSource(outline, completionOnly);
    }

    private async Task<DateTime?> ToLocalAsync(DateTime? utc)
        => utc is { } value ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))).DateTime : null;

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
