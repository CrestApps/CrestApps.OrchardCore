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
