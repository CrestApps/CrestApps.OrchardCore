using System.Collections.Concurrent;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Every write to an enrollment or its progress runs under one lock per enrollment and reads the enrollment again inside
/// it, so two requests can never both complete a course or overwrite each other.
/// </summary>
public sealed class EnrollmentWriteLockTests
{
    [Fact]
    public async Task EveryWritePath_TakesThePerEnrollmentLock()
    {
        // Arrange
        var locks = new RecordingLock();
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock>(locks);
        });
        await using var __ = harness;
        courses.AddCourse("course1", part => part.EnrollmentMode = EnrollmentMode.Approval);
        courses.AddItem("course1", "lesson");
        courses.AddItem("course1", "lesson2");
        var active = await EnrollAsync(harness, "active", "m-active");
        var pending = await EnrollAsync(harness, "pending", "m-pending", requiresApproval: true);
        var rejected = await EnrollAsync(harness, "rejected", "m-rejected", requiresApproval: true);
        var ended = await EnrollAsync(harness, "ended", "m-ended", LearningTestHarness.Now.AddMinutes(-1));

        // Act and assert
        async Task Expect(string enrollmentId, Func<IServiceProvider, Task> work)
        {
            locks.Keys.Clear();
            await harness.RunAsync(work);
            Assert.Contains("Learning:EnrollmentWrite:" + enrollmentId, locks.Keys);
        }

        await Expect(active.ItemId, services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(active, "lesson", 10));
        await Expect(active.ItemId, services => services.GetRequiredService<ILearnerProgressService>().ApproveCompletionAsync(active, "admin"));
        await Expect(pending.ItemId, services => services.GetRequiredService<IEnrollmentService>().ApproveAsync(pending.ItemId, "admin"));
        await Expect(rejected.ItemId, services => services.GetRequiredService<IEnrollmentService>().RejectAsync(rejected.ItemId, "admin", "Course full"));
        await Expect(active.ItemId, services => services.GetRequiredService<IEnrollmentService>().ExtendAsync(active.ItemId, LearningTestHarness.Now.AddDays(90), null, "admin", "More time"));
        await Expect(active.ItemId, services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(active.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));
        await Expect(ended.ItemId, services => services.GetRequiredService<IEnrollmentService>().ExpireEndedAsync());
        await Expect(pending.ItemId, services => services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningConstants.EnrollmentSources.Manual, "m-pending", "admin", "Refund"));
    }

    [Fact]
    public async Task TwoRequestsFinishingTheLastItemAtOnce_CompleteTheCourseOnce()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, KeyedLock>();
        });
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var enrollment = await EnrollAsync(harness, "learner", "m-learner");

        // Act
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => harness.RunAsync(services =>
            services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(enrollment, "lesson", 5)))));

        // Assert
        Assert.Single(observer.Events, name => name == "Completed");
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(enrollment.ItemId), commit: false);
        Assert.Equal(EnrollmentStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task TwoScopesEnrollingTheSameLearnerAtOnce_EndWithOneEnrollment_CarryingEveryGrant()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, KeyedLock>();
        });
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");

        // Act: a double click and an order arriving together, each in its own scope.
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() => harness.RunAsync(services =>
            services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = "learner",
                CourseId = "course1",
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "source-" + index,
            })))));

        // Assert
        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
        var stored = await harness.RunAsync(services => services.GetRequiredService<EnrollmentStore>().ListByUserAsync("learner"), commit: false);
        var enrollment = Assert.Single(stored);
        Assert.Equal(4, enrollment.Grants.Count);
    }

    [Fact]
    public async Task EnrollmentObservers_RunAfterTheEnrollmentIsCommitted()
    {
        // Arrange
        var committed = new CommitCheckingObserver();
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services => services.AddSingleton<IEnrollmentObserver>(committed));
        await using var __ = harness;
        committed.Harness = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");

        // Act
        var first = await EnrollAsync(harness, "learner", "m-1", requiresApproval: true);
        await EnrollAsync(harness, "learner", "m-2");

        // Assert: the request, and the activation by a second grant, were each heard after their write was committed.
        Assert.Equal(["Requested:True", "Status:True", "Enrolled:True"], committed.Seen);
        Assert.NotNull(first);
    }

    [Fact]
    public async Task TheUpkeep_RaisesANoticeUnderTheEnrollmentLock_AndAfterItsMarkerIsCommitted()
    {
        // Arrange
        var locks = new RecordingLock();
        var committed = new CommitCheckingObserver();
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock>(locks);
            services.AddSingleton<IEnrollmentObserver>(committed);
        });
        await using var __ = harness;
        committed.Harness = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson");
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "learner",
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-1",
            DueUtc = LearningTestHarness.Now.AddDays(2),
        }));
        committed.Seen.Clear();
        locks.Keys.Clear();

        // Act
        await harness.RunAsync(services => services.GetRequiredService<LearningUpkeep>().RunAsync());

        // Assert
        Assert.Contains(EnrollmentWriteGate.Key(result.Enrollment.ItemId), locks.Keys);
        Assert.Equal(["DueSoon:True"], committed.Seen);
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string sourceId, DateTime? accessEndsUtc = null, bool requiresApproval = false)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = sourceId,
            AccessEndsUtc = accessEndsUtc,
            RequiresApproval = requiresApproval,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }

    /// <summary>
    /// Records, for each event, whether another scope already reads the enrollment as the observer was given it: the
    /// same status and the same markers, which is true only once the write is committed.
    /// </summary>
    private sealed class CommitCheckingObserver : IEnrollmentObserver
    {
        public CalendarTestHarness Harness { get; set; }

        public List<string> Seen { get; } = [];

        public Task RequestedAsync(Enrollment enrollment, CancellationToken cancellationToken = default) => CheckAsync("Requested", enrollment);

        public Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default) => CheckAsync("Enrolled", enrollment);

        public Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default) => CheckAsync("Status", enrollment);

        public Task DueSoonAsync(Enrollment enrollment, CancellationToken cancellationToken = default) => CheckAsync("DueSoon", enrollment);

        private async Task CheckAsync(string name, Enrollment enrollment)
        {
            Enrollment stored;

            try
            {
                // Another scope, as a webhook sender or a notification job would read it; an open write must not block it
                // for long, so a timeout counts as not committed.
                stored = await Task.Run(() => Harness.RunAsync(async services => await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(enrollment.ItemId), commit: false))
                    .WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                stored = null;
            }

            var same = stored is not null
                && stored.Status == enrollment.Status
                && stored.Grants.Count == enrollment.Grants.Count
                && stored.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(enrollment.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal));

            Seen.Add(name + ":" + same);
        }
    }

    private sealed class RecordingLock : IDistributedLock
    {
        public ConcurrentBag<string> Keys { get; } = [];

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            Keys.Add(key);

            return Task.FromResult<ILocker>(new Releaser(null));
        }

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            Keys.Add(key);

            return Task.FromResult<(ILocker, bool)>((new Releaser(null), true));
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(false);
    }

    private sealed class KeyedLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            return new Releaser(semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            return await semaphore.WaitAsync(timeout) ? (new Releaser(semaphore), true) : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(_semaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);
    }

    private sealed class Releaser : ILocker
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            _semaphore?.Release();

            return ValueTask.CompletedTask;
        }

        public void Dispose()
            => _semaphore?.Release();
    }
}
