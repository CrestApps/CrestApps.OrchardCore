using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the course part: code, summary, enrollment, completion criteria, prerequisites and staff.
/// </summary>
internal sealed class CoursePartDisplayDriver : ContentPartDisplayDriver<CoursePart>
{
    private readonly CourseEditorOptions _editorOptions;

    internal readonly IStringLocalizer S;

    public CoursePartDisplayDriver(
        CourseEditorOptions editorOptions,
        IStringLocalizer<CoursePartDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CoursePart part, BuildPartEditorContext context)
    {
        return Initialize<CoursePartViewModel>(GetEditorShapeType(context), async model =>
        {
            var courseId = part.ContentItem.ContentItemId;

            model.Code = part.Code;
            model.Summary = part.Summary;
            model.Level = part.Level;
            model.EstimatedMinutes = part.EstimatedMinutes;
            model.EnrollmentMode = part.EnrollmentMode;
            model.HasEnrollmentKey = !string.IsNullOrEmpty(part.EnrollmentKeyHash);
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

        var keyNeeded = model.EnrollmentMode == EnrollmentMode.Key;
        var hasKey = !string.IsNullOrEmpty(part.EnrollmentKeyHash) && !model.ClearEnrollmentKey;

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

        if (!string.IsNullOrWhiteSpace(model.NewEnrollmentKey))
        {
            part.EnrollmentKeyHash = EnrollmentKeys.Hash(model.NewEnrollmentKey);
        }
        else if (model.ClearEnrollmentKey)
        {
            part.EnrollmentKeyHash = null;
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
