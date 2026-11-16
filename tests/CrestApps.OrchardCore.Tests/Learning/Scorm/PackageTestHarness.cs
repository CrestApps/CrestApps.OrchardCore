using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The Learning core with the packages core on a real store, in-memory courses, an in-memory package store, ephemeral
/// data protection and a clock the test moves.
/// </summary>
internal sealed class PackageTestHarness : IAsyncDisposable
{
    public const string Course = "course1";
    public const string Package = "package1";
    public const string Package2004 = "package2004";

    private PackageTestHarness(CalendarTestHarness inner, InMemoryCourses courses, InMemoryPackageFiles files, TestClock clock)
    {
        Inner = inner;
        Courses = courses;
        Files = files;
        Clock = clock;
    }

    public CalendarTestHarness Inner { get; }

    public InMemoryCourses Courses { get; }

    public InMemoryPackageFiles Files { get; }

    public TestClock Clock { get; }

    public static async Task<PackageTestHarness> CreateAsync(string completionMode = CompletionModes.PackageReported, decimal? masteryScore = 80, Action<CourseItemPart> configureItem = null, IDistributedLock distributedLock = null)
    {
        var files = new InMemoryPackageFiles();
        var clock = new TestClock { UtcNow = LearningTestHarness.Now };
        var protection = new EphemeralDataProtectionProvider();
        InMemoryCourses courses = null;

        var (inner, created, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddLearningPackagesCore();
                services.RemoveAll<IProtectedFileStore<LearningPackageFiles>>();
                services.AddSingleton<IProtectedFileStore<LearningPackageFiles>>(files);
                services.RemoveAll<IPackageContentSource>();
                services.AddSingleton<IPackageContentSource>(_ => new InMemoryPackageContent(courses));
                services.RemoveAll<IDataProtectionProvider>();
                services.AddSingleton<IDataProtectionProvider>(protection);

                if (distributedLock is not null)
                {
                    services.RemoveAll<IDistributedLock>();
                    services.AddSingleton(distributedLock);
                }
            },
            PackagesIndexSchema.CreateAsync,
            [new PackageRuntimeStateIndexProvider()],
            [PackagesConstants.CollectionName]);

        courses = created;

        var harness = new PackageTestHarness(inner, courses, files, clock);

        courses.AddCourse(Course);
        courses.AddItem(Course, "intro");
        await harness.AddPackageAsync(Package, completionMode, masteryScore, configureItem);

        return harness;
    }

    public async Task<ContentItem> AddPackageAsync(string id, string completionMode = CompletionModes.PackageReported, decimal? masteryScore = 80, Action<CourseItemPart> configureItem = null)
    {
        using var zip = PackageZip.SingleScoPackage();
        var file = await Files.SaveAsync("package.zip", zip);
        var item = Courses.AddItem(Course, id, kind: PackagesConstants.PackageKind, completionMode: completionMode, configure: configureItem);

        item.Alter<LearningPackagePart>(part =>
        {
            part.PackageFileId = file.FileId;
            part.Standard = PackagesConstants.Standards.Scorm12;
            part.LaunchPath = "index.html";
            part.MasteryScore = masteryScore;
            part.LaunchData = "level=2";
        });

        return item;
    }

    /// <summary>
    /// Adds a SCORM 2004 package item: a completion threshold of 0.8, a scaled passing score of 0.6, two objectives,
    /// launch data, a time limit and sequencing in its manifest.
    /// </summary>
    public async Task<ContentItem> AddScorm2004PackageAsync(string id = Package2004, string standard = PackagesConstants.Standards.Scorm2004FourthEdition, decimal? completionThreshold = 0.8m, decimal? scaledPassingScore = 0.6m, string completionMode = CompletionModes.PackageReported, Action<CourseItemPart> configureItem = null)
    {
        using var zip = PackageZip.SingleScoPackage();
        var file = await Files.SaveAsync("package2004.zip", zip);
        var item = Courses.AddItem(Course, id, kind: PackagesConstants.PackageKind, completionMode: completionMode, configure: configureItem);

        item.Alter<LearningPackagePart>(part =>
        {
            part.PackageFileId = file.FileId;
            part.Standard = standard;
            part.LaunchPath = "index.html";
            part.CompletionThreshold = completionThreshold;
            part.ScaledPassingScore = scaledPassingScore;
            part.ObjectiveIds = ["PRIMARY", "obj-2"];
            part.LaunchData = "chapter=1";
            part.MaxTimeAllowed = "PT30M";
            part.TimeLimitAction = "exit,message";
            part.HasSequencing = true;
        });

        return item;
    }

    public async Task<Enrollment> EnrollAsync(string userId)
    {
        var result = await Inner.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = Course,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }

    public Task ChangeEnrollmentAsync(string enrollmentId, Action<Enrollment> change)
        => Inner.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(enrollmentId);

            change(enrollment);
            await store.SaveAsync(enrollment);

            return true;
        });

    public Task<PackageLaunchResult> LaunchAsync(string userId, string itemId = Package)
        => Inner.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().LaunchAsync(userId, userId + " name", itemId));

    public Task<PackageRuntimeResult> ApplyAsync(string userId, PackageLaunchResult launch, bool commit = false, bool finish = false, string enrollmentId = null, string token = null, params (string Element, string Value)[] changes)
        => Inner.RunAsync(services => services.GetRequiredService<PackageRuntimeService>().ApplyAsync(new PackageRuntimeRequest
        {
            UserId = userId,
            EnrollmentId = enrollmentId ?? launch.Launch.EnrollmentId,
            PackageItemId = launch.Launch.PackageItemId,
            Token = token ?? launch.Token,
            Changes = changes.Select(change => new PackageRuntimeChange(change.Element, change.Value)).ToList(),
            Commit = commit,
            Finish = finish,
        }));

    public Task<PackageRuntimeState> StateAsync(string enrollmentId, string itemId = Package)
        => Inner.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().FindAsync(enrollmentId, itemId));

    public async Task<CourseItemProgress> ProgressAsync(string enrollmentId, string itemId = Package)
        => (await Inner.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId)))?.Find(itemId);

    public Task<Enrollment> FindEnrollmentAsync(string enrollmentId)
        => Inner.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId));

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Inner.RunAsync(work);

    public ValueTask DisposeAsync()
        => Inner.DisposeAsync();
}

/// <summary>
/// The package items of the in-memory courses.
/// </summary>
internal sealed class InMemoryPackageContent : IPackageContentSource
{
    private readonly InMemoryCourses _courses;

    public InMemoryPackageContent(InMemoryCourses courses)
    {
        _courses = courses;
    }

    public Task<ContentItem> GetPublishedAsync(string contentItemId)
    {
        try
        {
            return Task.FromResult(string.IsNullOrEmpty(contentItemId) ? null : _courses.Get(contentItemId));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<ContentItem>(null);
        }
    }
}
