using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Programs.Drivers;

/// <summary>
/// Edits the course sets of a learning path and shows its stages to learners with their progress.
/// </summary>
internal sealed class LearningPathPartDisplayDriver : ContentPartDisplayDriver<LearningPathPart>
{
    private readonly IContentManager _contentManager;
    private readonly ILearningPathService _paths;
    private readonly IEnrollmentStore _enrollments;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public LearningPathPartDisplayDriver(
        IContentManager contentManager,
        ILearningPathService paths,
        IEnrollmentStore enrollments,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LearningPathPartDisplayDriver> stringLocalizer)
    {
        _contentManager = contentManager;
        _paths = paths;
        _enrollments = enrollments;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(LearningPathPart part, BuildPartDisplayContext context)
        => Initialize<LearningPathPartViewModel>("LearningPathPart", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var completed = new HashSet<string>(StringComparer.Ordinal);

            model.PathId = part.ContentItem.ContentItemId;

            if (!string.IsNullOrEmpty(userId))
            {
                model.Progress = await _paths.FindProgressAsync(part.ContentItem.ContentItemId, userId);
                completed.UnionWith((await _enrollments.ListByUserAsync(userId))
                    .Where(enrollment => enrollment.Status == EnrollmentStatus.Completed)
                    .Select(enrollment => enrollment.CourseId));
                model.CanStart = part.AllowSelfStart && model.Progress is null && part.ContentItem.Published && await LearningPathAccess.AllCoursesOpenAsync(_contentManager, part);
            }

            var titles = (await _contentManager.GetAsync(LearningPathRules.CourseIds(part), VersionOptions.Published))
                .ToDictionary(item => item.ContentItemId, item => item.DisplayText, StringComparer.Ordinal);
            var available = titles.Keys.ToHashSet(StringComparer.Ordinal);
            var open = LearningPathRules.OpenStage(part, completed, available);

            model.Stages = LearningPathRules.Stages(part)
                .Select((stage, index) => new LearningPathStageViewModel
                {
                    Number = index + 1,
                    IsComplete = index < open,
                    IsOpen = index == open,
                    Sets = stage.Select(set => new LearningPathSetViewModel
                    {
                        Title = set.Title,
                        Connector = set.Connector,
                        Join = set.Join,
                        MinimumCount = set.MinimumCount,
                        IsComplete = LearningPathRules.IsSetComplete(set, completed, available),
                        Courses = set.CourseIds
                            .Where(courseId => titles.ContainsKey(courseId))
                            .Select(courseId => new LearningPathCourseViewModel { CourseId = courseId, Title = titles[courseId], IsCompleted = completed.Contains(courseId) })
                            .ToArray(),
                    }).ToArray(),
                })
                .ToArray();
        }).Location(OrchardCoreConstants.DisplayType.Detail, "Content:5");

    public override IDisplayResult Edit(LearningPathPart part, BuildPartEditorContext context)
        => Initialize<LearningPathPartEditViewModel>(GetEditorShapeType(context), async model =>
        {
            model.AllowSelfStart = part.AllowSelfStart;
            model.Sets = part.Sets.Select(set => new LearningPathSetEditViewModel
            {
                Id = set.Id,
                Title = set.Title,
                Connector = set.Connector,
                Join = set.Join,
                MinimumCount = set.MinimumCount,
                CourseIds = set.CourseIds.ToList(),
            }).ToList();
            // Only the chosen courses: the pickers search the rest on the server.
            model.CourseTitles = (await _contentManager.GetAsync(model.Sets.SelectMany(set => set.CourseIds).Distinct(StringComparer.Ordinal), VersionOptions.Latest))
                .ToDictionary(course => course.ContentItemId, course => course.DisplayText, StringComparer.Ordinal);
        });

    public override async Task<IDisplayResult> UpdateAsync(LearningPathPart part, UpdatePartEditorContext context)
    {
        var model = new LearningPathPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.Sets, value => value.AllowSelfStart);

        var sets = new List<LearningPathCourseSet>();

        foreach (var set in model.Sets ?? [])
        {
            var courseIds = set.CourseIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];

            if (courseIds.Count == 0 && string.IsNullOrWhiteSpace(set.Title))
            {
                continue;
            }

            if (courseIds.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Sets), S["The set '{0}' has no courses.", set.Title]);
            }

            if (set.Join == LearningPathSetJoin.AtLeast && (set.MinimumCount < 1 || set.MinimumCount > courseIds.Count))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Sets), S["The set '{0}' asks for between 1 and {1} courses.", set.Title ?? string.Empty, courseIds.Count]);
            }

            sets.Add(new LearningPathCourseSet
            {
                Id = string.IsNullOrWhiteSpace(set.Id) ? IdGenerator.GenerateId() : set.Id,
                Title = set.Title?.Trim(),
                Connector = sets.Count == 0 ? LearningPathConnector.Then : set.Connector,
                Join = set.Join,
                MinimumCount = set.Join == LearningPathSetJoin.AtLeast ? set.MinimumCount : 0,
                CourseIds = courseIds,
            });
        }

        part.Sets = sets;
        part.AllowSelfStart = model.AllowSelfStart;

        return Edit(part, context);
    }
}
