using CrestApps.OrchardCore.Learning.Programs.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Programs.ViewModels;

/// <summary>
/// The editor of a learning path's course sets.
/// </summary>
public class LearningPathPartEditViewModel
{
    public List<LearningPathSetEditViewModel> Sets { get; set; } = [];

    public bool AllowSelfStart { get; set; }

    [BindNever]
    public IList<SelectListItem> Courses { get; set; } = [];
}

/// <summary>
/// One course set in the editor.
/// </summary>
public class LearningPathSetEditViewModel
{
    public string Id { get; set; }

    public string Title { get; set; }

    public LearningPathConnector Connector { get; set; }

    public LearningPathSetJoin Join { get; set; }

    public int MinimumCount { get; set; }

    public List<string> CourseIds { get; set; } = [];
}

/// <summary>
/// The row model of the set editor's partial.
/// </summary>
public class LearningPathSetRowModel
{
    public string Prefix { get; set; }

    public string Index { get; set; }

    public bool IsFirst { get; set; }

    public LearningPathSetEditViewModel Set { get; set; }

    public IList<SelectListItem> Courses { get; set; } = [];
}

/// <summary>
/// A learning path as a learner sees it.
/// </summary>
public class LearningPathPartViewModel
{
    public string PathId { get; set; }

    public IReadOnlyList<LearningPathStageViewModel> Stages { get; set; } = [];

    public LearningPathProgress Progress { get; set; }

    public bool CanStart { get; set; }
}

/// <summary>
/// A stage of a learning path.
/// </summary>
public class LearningPathStageViewModel
{
    public int Number { get; set; }

    public bool IsOpen { get; set; }

    public bool IsComplete { get; set; }

    public IReadOnlyList<LearningPathSetViewModel> Sets { get; set; } = [];
}

/// <summary>
/// A course set of a learning path.
/// </summary>
public class LearningPathSetViewModel
{
    public string Title { get; set; }

    public LearningPathConnector Connector { get; set; }

    public LearningPathSetJoin Join { get; set; }

    public int MinimumCount { get; set; }

    public bool IsComplete { get; set; }

    public IReadOnlyList<LearningPathCourseViewModel> Courses { get; set; } = [];
}

/// <summary>
/// A course of a learning path.
/// </summary>
public class LearningPathCourseViewModel
{
    public string CourseId { get; set; }

    public string Title { get; set; }

    public bool IsCompleted { get; set; }
}

/// <summary>
/// The people on a learning path.
/// </summary>
public class LearningPathPeopleViewModel
{
    public string PathId { get; set; }

    public string PathTitle { get; set; }

    public LearningPathState? State { get; set; }

    public AdminListContext<LearningPathProgress> List { get; set; }
}

/// <summary>
/// A row of the people on a learning path.
/// </summary>
public class LearningPathProgressRowViewModel
{
    public LearningPathProgress Progress { get; set; }

    public string UserName { get; set; }

    public int StageCount { get; set; }
}

/// <summary>
/// The list of required learning rules.
/// </summary>
public class RequiredLearningListViewModel
{
    public string Search { get; set; }

    public AdminListContext<LearningRequirement> List { get; set; }
}

/// <summary>
/// A row of the required learning list.
/// </summary>
public class LearningRequirementRowViewModel
{
    public LearningRequirement Requirement { get; set; }

    public string TargetKindName { get; set; }

    public string TargetName { get; set; }

    public string AudienceName { get; set; }
}

/// <summary>
/// The editor of a required learning rule.
/// </summary>
public class RequiredLearningEditViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public bool Enabled { get; set; } = true;

    public string TargetKind { get; set; }

    public string TargetId { get; set; }

    public string AudienceName { get; set; }

    public string UserNames { get; set; }

    public RequiredLearningDueRule DueRule { get; set; }

    public DateOnly? DueDate { get; set; }

    public int DueDays { get; set; } = 30;

    public string ReminderDays { get; set; } = "7, 1";

    public int? EscalateAfterDays { get; set; }

    [BindNever]
    public IReadOnlyList<RequiredLearningTargetChoice> Targets { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Audiences { get; set; } = [];
}

/// <summary>
/// A kind of target with its options, in the editor.
/// </summary>
public class RequiredLearningTargetChoice
{
    public string Kind { get; set; }

    public string DisplayName { get; set; }

    public IList<SelectListItem> Options { get; set; } = [];
}

/// <summary>
/// The people under a required learning rule.
/// </summary>
public class RequiredLearningPeopleViewModel
{
    public LearningRequirement Requirement { get; set; }

    public string TargetName { get; set; }

    public RequiredLearningState? State { get; set; }

    public bool CanExempt { get; set; }

    public string LastRun { get; set; }

    public AdminListContext<RequiredLearningAssignment> List { get; set; }
}

/// <summary>
/// A row of the people under a required learning rule.
/// </summary>
public class RequiredLearningAssignmentRowViewModel
{
    public RequiredLearningAssignment Assignment { get; set; }

    public string UserName { get; set; }

    public string Due { get; set; }

    public string Completed { get; set; }

    public bool CanExempt { get; set; }
}

/// <summary>
/// Who a rule would cover.
/// </summary>
public class RequiredLearningPreviewViewModel
{
    public LearningRequirement Requirement { get; set; }

    public int Count { get; set; }

    public AdminListContext<RequiredLearningPreviewRow> List { get; set; }
}

/// <summary>
/// A person a rule would cover.
/// </summary>
public class RequiredLearningPreviewRow
{
    public string UserId { get; set; }

    public string UserName { get; set; }

    public bool AlreadyRequired { get; set; }
}

/// <summary>
/// The form that exempts a person.
/// </summary>
public class RequiredLearningExemptViewModel
{
    public string AssignmentId { get; set; }

    public string Reason { get; set; }

    [BindNever]
    public string UserName { get; set; }

    [BindNever]
    public string RequirementName { get; set; }

    [BindNever]
    public string RequirementId { get; set; }
}

/// <summary>
/// The learner's required learning and learning paths.
/// </summary>
public class MyRequiredLearningViewModel
{
    public IReadOnlyList<MyRequiredLearningItem> Required { get; set; } = [];

    public IReadOnlyList<MyLearningPathItem> Paths { get; set; } = [];
}

/// <summary>
/// One piece of required learning of the learner.
/// </summary>
public class MyRequiredLearningItem
{
    public RequiredLearningAssignment Assignment { get; set; }

    public string Name { get; set; }

    public string TargetKind { get; set; }

    public string TargetId { get; set; }

    public string Due { get; set; }
}

/// <summary>
/// One learning path of the learner.
/// </summary>
public class MyLearningPathItem
{
    public LearningPathProgress Progress { get; set; }

    public string Title { get; set; }

    public string Url { get; set; }

    public int StageCount { get; set; }
}

/// <summary>
/// The required learning of a manager's team.
/// </summary>
public class TeamLearningViewModel
{
    public bool IsManager { get; set; }

    public AdminListContext<TeamLearningRow> List { get; set; }
}

/// <summary>
/// A person of the team with their required learning.
/// </summary>
public class TeamLearningRow
{
    public string UserId { get; set; }

    public string Name { get; set; }

    public string JobTitle { get; set; }

    public IReadOnlyList<TeamLearningItem> Items { get; set; } = [];
}

/// <summary>
/// One piece of required learning of a person of the team.
/// </summary>
public class TeamLearningItem
{
    public string Name { get; set; }

    public RequiredLearningState State { get; set; }

    public string Due { get; set; }
}
