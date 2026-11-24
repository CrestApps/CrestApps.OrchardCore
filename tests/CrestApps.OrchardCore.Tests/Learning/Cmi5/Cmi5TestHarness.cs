using System.Text.Json.Nodes;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using CrestApps.OrchardCore.Tests.Learning.Scorm;
using CrestApps.OrchardCore.Tests.Learning.Xapi;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Cmi5;

/// <summary>
/// The Learning core, the packages core with cmi5 and the LRS core on one real store, in-memory courses and package
/// files, ephemeral data protection and a clock the test moves. The course has one cmi5 package item with two AUs: a
/// relative one with moveOn Passed and a mastery score of 0.8, and an absolute one with moveOn Completed.
/// </summary>
internal sealed class Cmi5TestHarness : IAsyncDisposable
{
    public const string Course = "course1";
    public const string Package = "cmi5pkg";
    public const string HomePage = "http://localhost";
    public const string CourseId = "https://example.com/courses/forklift";
    public const string FirstAu = "https://example.com/courses/forklift/au/quiz";
    public const string SecondAu = "https://example.com/courses/forklift/au/video";

    private Cmi5TestHarness(CalendarTestHarness inner, InMemoryCourses courses, TestClock clock)
    {
        Inner = inner;
        Courses = courses;
        Clock = clock;
    }

    public CalendarTestHarness Inner { get; }

    public InMemoryCourses Courses { get; }

    public TestClock Clock { get; }

    public static async Task<Cmi5TestHarness> CreateAsync(string completionMode = CompletionModes.PackageReported, Action<Cmi5Course> configureCourse = null)
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
                services.AddHttpContextAccessor();
                services.AddLearningPackagesCore();
                CrestApps.OrchardCore.Tests.Learning.Scorm.PackageArchiveCacheTestServices.Use(services);
                services.AddLearningXapiCore();
                services.AddLearningCmi5Core();
                services.RemoveAll<IProtectedFileStore<LearningPackageFiles>>();
                services.AddSingleton<IProtectedFileStore<LearningPackageFiles>>(files);
                services.RemoveAll<IProtectedFileStore<XapiAttachmentFiles>>();
                services.AddSingleton<IProtectedFileStore<XapiAttachmentFiles>>(new InMemoryProtectedFiles<XapiAttachmentFiles>());
                services.RemoveAll<IPackageContentSource>();
                services.AddSingleton<IPackageContentSource>(_ => new InMemoryPackageContent(courses));
                services.RemoveAll<IDataProtectionProvider>();
                services.AddSingleton<IDataProtectionProvider>(protection);
            },
            async schemaBuilder =>
            {
                await PackagesIndexSchema.CreateAsync(schemaBuilder);
                await Cmi5IndexSchema.CreateAsync(schemaBuilder);
                await XapiIndexSchema.CreateAllAsync(schemaBuilder);
            },
            [new PackageRuntimeStateIndexProvider(), new Cmi5RegistrationIndexProvider(), .. XapiIndexSchema.IndexProviders()],
            [PackagesConstants.CollectionName, XapiConstants.CollectionName]);

        courses = created;

        var harness = new Cmi5TestHarness(inner, courses, clock);

        courses.AddCourse(Course);
        courses.AddItem(Course, "intro");

        var course = SampleCourse();

        configureCourse?.Invoke(course);

        var item = courses.AddItem(Course, Package, kind: PackagesConstants.PackageKind, completionMode: completionMode);
        var file = await files.SaveAsync("cmi5.zip", new MemoryStream([1, 2, 3]));

        item.Alter<LearningPackagePart>(part =>
        {
            part.PackageFileId = file.FileId;
            part.Standard = PackagesConstants.Standards.Cmi5;
            part.Title = course.Title;
            part.LaunchPath = "quiz/index.html";
            part.Cmi5 = course;
        });

        return harness;
    }

    public static Cmi5Course SampleCourse()
        => new()
        {
            Id = CourseId,
            Title = "Forklift safety",
            Blocks = [new Cmi5Block { Id = "https://example.com/courses/forklift/block/1", Title = "Practice" }],
            Aus =
            [
                new Cmi5Au
                {
                    Id = FirstAu,
                    Title = "Quiz",
                    Url = "quiz/index.html?lang=en",
                    Path = "quiz/index.html",
                    Query = "lang=en",
                    MoveOn = "Passed",
                    MasteryScore = 0.8m,
                    LaunchParameters = "level=2",
                    BlockIds = ["https://example.com/courses/forklift/block/1"],
                },
                new Cmi5Au
                {
                    Id = SecondAu,
                    Title = "Video",
                    Url = "https://media.example.com/video.html",
                    IsAbsolute = true,
                    MoveOn = "Completed",
                    LaunchMethod = "OwnWindow",
                },
            ],
        };

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

    public Task<Cmi5LaunchResult> LaunchAsync(string userId, int au = 0)
        => Inner.RunAsync(services => services.GetRequiredService<Cmi5LaunchService>().LaunchAsync(new Cmi5LaunchRequest
        {
            UserId = userId,
            UserName = userId + " name",
            PackageItemId = Package,
            AuIndex = au,
            EndpointUrl = HomePage + "/learning/xapi/",
            FetchUrlBase = HomePage + "/learning/cmi5/fetch/",
            ReturnUrl = HomePage + "/learning/cmi5/return",
            RunPathBase = "/learning/packages/run/",
            Language = "fr-FR",
        }));

    public Task<Cmi5FetchResult> FetchAsync(string launchUrl)
        => Inner.RunAsync(services => services.GetRequiredService<Cmi5LaunchService>().FetchAsync(FetchToken(launchUrl)));

    public async Task<XapiCredential> CredentialAsync(Cmi5FetchResult fetch)
    {
        var value = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fetch.AuthToken));
        var token = value[(value.IndexOf(':', StringComparison.Ordinal) + 1)..];
        var scope = await Inner.RunAsync(services => Task.FromResult(services.GetRequiredService<XapiLaunchTokens>().Read(token)));

        return scope is null
            ? null
            : new XapiCredential
            {
                AuthorityKey = "launch:cmi5",
                Authority = new JsonObject { ["objectType"] = "Agent", ["account"] = new JsonObject { ["homePage"] = HomePage, ["name"] = "launch:cmi5" } },
                Launch = scope,
            };
    }

    public Task<XapiResponse> LrsAsync(XapiRequest request, XapiCredential credential)
        => Inner.RunAsync(services => services.GetRequiredService<XapiEndpointHandler>().HandleAsync(request, credential), commit: false);

    public Task<PackageRuntimeState> StateAsync(string enrollmentId)
        => Inner.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().FindAsync(enrollmentId, Package));

    public async Task<CourseItemProgress> ProgressAsync(string enrollmentId)
        => (await Inner.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId)))?.Find(Package);

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Inner.RunAsync(work);

    public static Dictionary<string, string> LaunchParameters(string launchUrl)
    {
        var query = launchUrl[(launchUrl.IndexOf('?', StringComparison.Ordinal) + 1)..];

        return query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty, StringComparer.Ordinal);
    }

    public static string FetchToken(string launchUrl)
    {
        var fetch = LaunchParameters(launchUrl)["fetch"];

        return Uri.UnescapeDataString(fetch[(fetch.LastIndexOf('/') + 1)..]);
    }

    public ValueTask DisposeAsync()
        => Inner.DisposeAsync();
}
