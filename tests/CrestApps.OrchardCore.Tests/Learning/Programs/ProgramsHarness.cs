using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Core.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Learning with paths and required learning on a SQLite store: courses in memory, learning paths as content items a
/// fake content manager returns, audiences whose members the test sets, and a clock the test moves.
/// </summary>
internal sealed class ProgramsHarness
{
    private readonly Dictionary<string, ContentItem> _paths = new(StringComparer.Ordinal);

    private ProgramsHarness(AssessmentTestHarness inner)
    {
        Inner = inner;
    }

    public AssessmentTestHarness Inner { get; }

    public TestClock Clock => Inner.Clock;

    public Dictionary<string, List<string>> Audiences { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RecordingRequiredLearningObserver Observer { get; } = new();

    public static async Task<ProgramsHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        ProgramsHarness harness = null;

        var inner = await AssessmentTestHarness.CreateAsync(
            services =>
            {
                services.AddLearningPathsCore();
                services.AddRequiredLearningCore();
                services.AddSingleton<ILocalClock>(provider => new UtcLocalClock(provider.GetRequiredService<IClock>()));
                services.TryAddSingleton(Mock.Of<IContentDefinitionManager>());
                services.RemoveAll<IContentManager>();
                services.AddSingleton(provider => harness.ContentManager(provider));
                services.AddSingleton<IUserAudienceService>(provider => new FakeAudiences(harness.Audiences));
                services.AddSingleton(provider => Users());
                services.AddSingleton<IRequiredLearningObserver>(provider => harness.Observer);
                configure?.Invoke(services);
            },
            ProgramsIndexSchema.CreateAsync,
            [new LearningPathProgressIndexProvider(), new LearningRequirementIndexProvider(), new RequiredLearningAssignmentIndexProvider()],
            [ProgramsConstants.CollectionName]);

        harness = new ProgramsHarness(inner);

        foreach (var course in new[] { "c1", "c2", "c3" })
        {
            inner.Courses.AddCourse(course);
            inner.Courses.AddItem(course, course + "-lesson");
        }

        return harness;
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => Inner.RunAsync(work);

    public Task RunAsync(Func<IServiceProvider, Task> work) => Inner.RunAsync<object>(async services =>
    {
        await work(services);

        return null;
    });

    public void AddPath(string id, params LearningPathCourseSet[] sets)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = ProgramsConstants.LearningPathContentType, DisplayText = id, Published = true, Latest = true };

        item.Apply(new LearningPathPart { Sets = sets.ToList() });
        _paths[id] = item;
    }

    public ContentItem Path(string id) => _paths[id];

    public async Task CompleteCourseAsync(string userId, string courseId)
    {
        var enrollment = (await EnrollmentsAsync(userId, courseId)).FirstOrDefault(item => item.Status != EnrollmentStatus.Completed)
            ?? await Inner.EnrollAsync(userId, courseId);

        await RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);

            return await services.GetRequiredService<ILearnerProgressService>().RecordViewedAsync(fresh, courseId + "-lesson", 100);
        });
    }

    public Task<IReadOnlyList<Enrollment>> EnrollmentsAsync(string userId, string courseId)
        => RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync(userId, courseId));

    private IContentManager ContentManager(IServiceProvider provider)
    {
        var manager = new Mock<IContentManager>();

        manager.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => _paths.TryGetValue(id, out var path) ? path : FindCourse(id));

        return manager.Object;
    }

    private ContentItem FindCourse(string id)
    {
        try
        {
            return Inner.Courses.Get(id);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static UserManager<IUser> Users()
    {
        var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        users.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => Mock.Of<IUser>(user => user.UserName == id));

        return users.Object;
    }

    private sealed class FakeAudiences : IUserAudienceService
    {
        private readonly Dictionary<string, List<string>> _audiences;

        public FakeAudiences(Dictionary<string, List<string>> audiences)
        {
            _audiences = audiences;
        }

        public Task<IReadOnlyList<UserAudienceSummary>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserAudienceSummary>>(_audiences.Keys.Select(name => new UserAudienceSummary(name, name, true)).ToArray());

        public Task<UserAudienceDefinition> FindDefinitionAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<UserAudienceDefinition>(null);

        public Task<IReadOnlyList<string>> SaveDefinitionAsync(UserAudienceDefinition definition, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> DeleteDefinitionAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<(int Count, IReadOnlyList<string> UserIds)> GetMembersAsync(string name, int skip, int take, CancellationToken cancellationToken = default)
        {
            var members = _audiences.TryGetValue(name, out var list) ? list : [];

            return Task.FromResult<(int, IReadOnlyList<string>)>((members.Count, members.Skip(skip).Take(take).ToArray()));
        }

        public Task<bool> IsMemberAsync(string name, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_audiences.TryGetValue(name, out var list) && list.Contains(userId));
    }

    private sealed class UtcLocalClock : ILocalClock
    {
        private readonly IClock _clock;

        public UtcLocalClock(IClock clock)
        {
            _clock = clock;
        }

        public Task<DateTimeOffset> GetLocalNowAsync() => Task.FromResult(new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero));

        public Task<ITimeZone> GetLocalTimeZoneAsync() => Task.FromResult<ITimeZone>(null);

        public Task<DateTimeOffset> ConvertToLocalAsync(DateTimeOffset dateTimeOffset) => Task.FromResult(dateTimeOffset);

        public Task<DateTime> ConvertToUtcAsync(DateTime dateTime) => Task.FromResult(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
    }
}

/// <summary>
/// Records what required learning tells its observers.
/// </summary>
internal sealed class RecordingRequiredLearningObserver : IRequiredLearningObserver
{
    public List<string> Events { get; } = [];

    public Task RequiredAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Record($"required:{assignment.UserId}");

    public Task DueSoonAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, int daysBefore, CancellationToken cancellationToken = default)
        => Record($"due-soon:{assignment.UserId}:{daysBefore}");

    public Task OverdueAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Record($"overdue:{assignment.UserId}");

    public Task ExemptedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Record($"exempted:{assignment.UserId}");

    public Task CompletedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Record($"completed:{assignment.UserId}");

    private Task Record(string value)
    {
        lock (Events)
        {
            Events.Add(value);
        }

        return Task.CompletedTask;
    }
}
