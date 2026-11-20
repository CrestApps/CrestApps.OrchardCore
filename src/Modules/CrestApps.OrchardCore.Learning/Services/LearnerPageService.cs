using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Builds the learner pages: the player, My learning and the transcript.
/// </summary>
public sealed class LearnerPageService
{
    private readonly ICourseStructureService _courseStructure;
    private readonly ICourseAccessService _courseAccess;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILearnerProgressService _progressService;
    private readonly CourseReleaseEvaluator _releaseEvaluator;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IContentManager _contentManager;
    private readonly CourseItemKindOptions _kindOptions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerPageService"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="courseAccess">The course access decision.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressService">The progress service.</param>
    /// <param name="releaseEvaluator">The release rule evaluator.</param>
    /// <param name="contentItemDisplayManager">The content item display manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="kindOptions">The course item kinds.</param>
    /// <param name="localizerFactory">The string localizer factory.</param>
    /// <param name="clock">The clock.</param>
    public LearnerPageService(
        ICourseStructureService courseStructure,
        ICourseAccessService courseAccess,
        IEnrollmentStore enrollmentStore,
        ILearnerProgressService progressService,
        CourseReleaseEvaluator releaseEvaluator,
        IContentItemDisplayManager contentItemDisplayManager,
        IContentManager contentManager,
        IOptions<CourseItemKindOptions> kindOptions,
        IStringLocalizerFactory localizerFactory,
        IClock clock)
    {
        _courseStructure = courseStructure;
        _courseAccess = courseAccess;
        _enrollmentStore = enrollmentStore;
        _progressService = progressService;
        _releaseEvaluator = releaseEvaluator;
        _contentItemDisplayManager = contentItemDisplayManager;
        _contentManager = contentManager;
        _kindOptions = kindOptions.Value;
        _localizerFactory = localizerFactory;
        _clock = clock;
    }

    /// <summary>
    /// Builds the player for a course item, or for the first open item of the course.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="itemId">The item, or <see langword="null"/> for the first open item.</param>
    /// <param name="updater">The model updater of the request.</param>
    /// <returns>The player, or <see langword="null"/> when the course or the item does not exist.</returns>
    public async Task<PlayerViewModel> BuildPlayerAsync(ClaimsPrincipal user, string courseId, string itemId, IUpdateModel updater)
    {
        var course = await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return null;
        }

        var outline = await _courseStructure.GetOutlineAsync(course);

        if (outline.Entries.Count == 0)
        {
            return new PlayerViewModel { Outline = outline, Access = CourseItemAccessReason.NotReleased, KindIcons = KindIcons(), KindNames = KindNames() };
        }

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var enrollments = string.IsNullOrEmpty(userId) ? [] : await _enrollmentStore.ListByCourseForUserAsync(userId, courseId);
        var enrollment = enrollments.FirstOrDefault(candidate => candidate.IsOpen) ?? enrollments.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Completed);
        var progress = enrollment is null ? new EnrollmentProgress() : await _progressService.GetAsync(enrollment.ItemId);
        var model = new PlayerViewModel
        {
            Outline = outline,
            Enrollment = enrollment,
            Progress = progress,
            KindIcons = KindIcons(),
            KindNames = KindNames(),
        };

        model.LockedItems = await GetLocksAsync(course, outline, enrollment, progress);

        // The player steps through the items learners open; text shown in place is not a step of its own.
        var steps = outline.Entries.Where(entry => !entry.Inline).ToList();
        var current = string.IsNullOrEmpty(itemId)
            ? steps.FirstOrDefault(entry => progress.Find(entry.Item.ContentItemId)?.State != CourseItemState.Completed && !model.LockedItems.ContainsKey(entry.Item.ContentItemId)) ?? steps.FirstOrDefault() ?? outline.Entries[0]
            : outline.Find(itemId);

        if (current is null)
        {
            return null;
        }

        var index = steps.IndexOf(current);

        model.Current = current;
        model.CurrentProgress = progress.Find(current.Item.ContentItemId);
        model.PreviousItemId = index > 0 ? steps[index - 1].Item.ContentItemId : null;
        model.NextItemId = index >= 0 && index < steps.Count - 1 ? steps[index + 1].Item.ContentItemId : null;

        // The text just before the item in its section shows above it, as on the course page.
        var position = outline.Entries.IndexOf(current);

        for (var before = position - 1; before >= 0 && outline.Entries[before].Inline && outline.Entries[before].Section == current.Section; before--)
        {
            if (!model.LockedItems.ContainsKey(outline.Entries[before].Item.ContentItemId))
            {
                model.TextBefore.Insert(0, outline.Entries[before]);
            }
        }

        // Text that ends a section, after its last item, shows under that item: the outline is navigation, not content.
        var lastStepOfSection = !outline.Entries.Skip(position + 1).Any(entry => entry.Section == current.Section && !entry.Inline);

        if (lastStepOfSection)
        {
            for (var after = position + 1; after < outline.Entries.Count && outline.Entries[after].Inline && outline.Entries[after].Section == current.Section; after++)
            {
                if (!model.LockedItems.ContainsKey(outline.Entries[after].Item.ContentItemId))
                {
                    model.TextAfter.Add(outline.Entries[after]);
                }
            }
        }

        model.StepNumber = index + 1;
        model.StepCount = steps.Count;
        model.RequiredCount = outline.RequiredEntries.Count();
        model.CompletedCount = outline.RequiredEntries.Count(entry => progress.Find(entry.Item.ContentItemId)?.State == CourseItemState.Completed);

        var access = await _courseAccess.GetItemAccessAsync(user, current.Item);

        model.Access = access.Reason;
        model.LockedReason = access.LockedReason;
        model.AvailableFromUtc = access.AvailableFromUtc;
        model.BlockingItemId = access.BlockingItemId;
        model.TracksProgress = !current.Inline && access.Reason == CourseItemAccessReason.Enrolled && enrollment is { Status: EnrollmentStatus.Active } && enrollment.UserId == userId;

        if (access.Allowed)
        {
            model.CurrentShape = await _contentItemDisplayManager.BuildDisplayAsync(current.Item, updater, "Detail");

            // The player shows the item's title once, in its own header with the item's kind and completion condition.
            if (model.CurrentShape is IZoneHolding zones && zones.Zones["Header"] is Shape header)
            {
                header.Remove("TitlePart");
            }
        }

        return model;
    }

    /// <summary>
    /// Decides, for each item of the outline, whether it is locked to a learner with an active enrollment, and why.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <param name="outline">The outline of the course.</param>
    /// <param name="enrollment">The learner's enrollment, or <see langword="null"/>.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>The locked items, by content item id, with the decision that keeps each locked.</returns>
    public async Task<IDictionary<string, ReleaseDecision>> GetLocksAsync(ContentItem course, CourseOutline outline, Enrollment enrollment, EnrollmentProgress progress)
    {
        var locks = new Dictionary<string, ReleaseDecision>(StringComparer.Ordinal);

        if (enrollment is not { Status: EnrollmentStatus.Active })
        {
            return locks;
        }

        var coursePart = course.Get<CoursePart>(nameof(CoursePart));

        if (CourseSchedules.Of(coursePart, _clock.UtcNow) == CourseSchedule.NotStarted)
        {
            // Before the course starts, every item waits for its start date.
            foreach (var entry in outline.Entries)
            {
                locks[entry.Item.ContentItemId] = ReleaseDecision.Locked(LearningRefusals.CourseNotStarted, coursePart?.StartsUtc);
            }

            return locks;
        }

        foreach (var entry in outline.Entries)
        {
            var release = await _releaseEvaluator.EvaluateAsync(entry, enrollment, progress);

            if (!release.IsReleased)
            {
                locks[entry.Item.ContentItemId] = release;
            }
        }

        return locks;
    }

    /// <summary>
    /// Builds My learning for a learner.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <returns>The page.</returns>
    public async Task<MyLearningViewModel> BuildMyLearningAsync(string userId)
    {
        var model = new MyLearningViewModel();
        var seenOpen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var enrollment in await _enrollmentStore.ListByUserAsync(userId))
        {
            var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
            var coursePart = course?.Get<CoursePart>(nameof(CoursePart));
            var row = new LearnerCourseViewModel
            {
                StartsUtc = coursePart?.StartsUtc,
                EndsUtc = coursePart?.EndsUtc,
                Schedule = CourseSchedules.Of(coursePart, _clock.UtcNow),
                Enrollment = enrollment,
                Title = course?.DisplayText ?? enrollment.Completion?.CourseTitle ?? enrollment.CourseId,
                Code = (course is not null && course.TryGet<CoursePart>(out var part) ? part.Code : null) ?? enrollment.Completion?.CourseCode,
                CourseAvailable = course is not null,
                CourseRouteValues = course is null ? null : (await _contentManager.PopulateAspectAsync<ContentItemMetadata>(course)).DisplayRouteValues,
            };

            if (enrollment.Status == EnrollmentStatus.Completed)
            {
                model.Completed.Add(row);
            }
            else if (enrollment.IsOpen && seenOpen.Add(enrollment.CourseId))
            {
                // As in Moodle, a course whose end date has passed is a past course, not one in progress; it stays open.
                if (row.Schedule == CourseSchedule.Ended)
                {
                    model.Past.Add(row);
                }
                else
                {
                    model.Current.Add(row);
                }
            }
            else if (enrollment.Status == EnrollmentStatus.Expired)
            {
                model.Other.Add(row);
            }
        }

        return model;
    }

    /// <summary>
    /// Lists a learner's completed enrollments, newest first, for the transcript.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <returns>The completed enrollments.</returns>
    public async Task<IReadOnlyList<Enrollment>> GetTranscriptAsync(string userId)
    {
        return (await _enrollmentStore.ListByUserAsync(userId))
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Completed && enrollment.Completion is not null)
            .OrderByDescending(enrollment => enrollment.CompletedUtc)
            .ToArray();
    }

    /// <summary>
    /// Gets the name of each course item kind, as learners read it.
    /// </summary>
    /// <returns>The names, by kind.</returns>
    public Dictionary<string, string> KindNames()
        => _kindOptions.Kinds.Where(kind => kind.DisplayName is not null).ToDictionary(kind => kind.Name, kind => _localizerFactory.Localize(kind.DisplayName).Value, StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, string> KindIcons()
        => _kindOptions.Kinds.ToDictionary(kind => kind.Name, kind => kind.Icon, StringComparer.OrdinalIgnoreCase);
}
