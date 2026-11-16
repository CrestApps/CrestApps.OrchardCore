using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Abstractions.Setup;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Media;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Setup.Services;
using OrchardCore.Title.Models;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// One real Orchard Core host for the Learning suite: the Default tenant set up blank with Learning, the learner pages,
/// Secure Media and GraphQL; a published course with a page lesson and a file lesson whose attached file is in the
/// media store; a learner who is enrolled and one who is not.
/// </summary>
public sealed partial class LearningSiteFixture : IAsyncLifetime
{
    /// <summary>
    /// The password of every account the fixture creates.
    /// </summary>
    public const string Password = "Password1!";

    /// <summary>
    /// The text of the page lesson's body, which only enrolled learners may read.
    /// </summary>
    public const string SecretLessonText = "Exit routes are posted beside every lift";

    private static readonly string[] _features =
    [
        "OrchardCore.Recipes",
        "OrchardCore.Media.Security",
        "OrchardCore.Apis.GraphQL",
        "OrchardCore.Feeds",
        "OrchardCore.Search",
        "OrchardCore.Lucene",
        "OrchardCore.Queries.Sql",
        "OrchardCore.Themes",
        LearningConstants.Features.Learning,
        LearningConstants.Features.LearnerPages,
        LearningConstants.Features.SelfEnrollment,
        "CrestApps.OrchardCore.Learning.Assessments",
        "CrestApps.OrchardCore.Learning.Assessments.Gradebook",
        "CrestApps.OrchardCore.Learning.Certifications",
        CrestApps.OrchardCore.Users.UsersConstants.Feature.Audiences,
        CrestApps.OrchardCore.Learning.Programs.ProgramsConstants.Features.Paths,
        CrestApps.OrchardCore.Learning.Programs.ProgramsConstants.Features.RequiredLearning,
        CrestApps.OrchardCore.Workforce.WorkforceConstants.Features.TimeOff,
        CrestApps.OrchardCore.Learning.Commerce.LearningCommerceConstants.Feature,
        CrestApps.OrchardCore.Learning.Classes.ClassesConstants.Feature,
        CrestApps.OrchardCore.Learning.Coursework.CourseworkConstants.Feature,
        CrestApps.OrchardCore.Learning.Packages.PackagesConstants.Features.Scorm,
        "OrchardCore.OpenId",
        "OrchardCore.OpenId.Management",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation",
        "OrchardCore.Secrets",
        CrestApps.OrchardCore.Learning.Xapi.XapiConstants.Features.LearningRecordStore,
        CrestApps.OrchardCore.Learning.Packages.PackagesConstants.Features.Cmi5,
    ];

    private WebApplication _application;
    private string _applicationDataPath;

    public IShellHost ShellHost { get; private set; }

    public Uri BaseAddress { get; private set; }

    public string CourseId { get; private set; }

    public string PageLessonId { get; private set; }

    public string FileLessonId { get; private set; }

    public string EnrollmentId { get; private set; }

    public string EnrolledUserId { get; private set; }

    /// <summary>
    /// Gets a learner no test enrolls in the fixture's set-up, for tests that enroll someone.
    /// </summary>
    public string SpareLearnerId { get; private set; }

    /// <summary>
    /// Gets the instructor of the course, who holds Manage own courses.
    /// </summary>
    public string InstructorId { get; private set; }

    /// <summary>
    /// Gets a published course without staff.
    /// </summary>
    public string OtherCourseId { get; private set; }

    /// <summary>
    /// Gets the path of the file attached to the file lesson.
    /// </summary>
    public string LessonFilePath => $"media/mediafields/{LearningConstants.ContentTypes.FileLesson}/{FileLessonId}/guide.txt";

    public async ValueTask InitializeAsync()
    {
        _applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-learning-{Guid.NewGuid():N}");
        var webRootPath = Path.Combine(_applicationDataPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LearningSiteFixture).Assembly.FullName,
            ContentRootPath = _applicationDataPath,
            EnvironmentName = Environments.Development,
            WebRootPath = webRootPath,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["OrchardCore:OrchardCore_Documents:CheckConcurrency"] = bool.FalseString;
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddOrchardCms();
        builder.Services.Configure<ShellOptions>(options => options.ShellsApplicationDataPath = _applicationDataPath);

        _application = builder.Build();
        _application.UseOrchardCore();
        await _application.StartAsync();

        ShellHost = _application.Services.GetRequiredService<IShellHost>();
        await ShellHost.InitializeAsync();

        var port = new Uri(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First()).Port;
        BaseAddress = new Uri($"http://127.0.0.1:{port}/");

        await SetupAsync();
        await InTenantAsync(async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var available = await features.GetAvailableFeaturesAsync();

            await features.EnableFeaturesAsync(available.Where(feature => _features.Contains(feature.Id)), force: true);
        });

        // Everyone may run GraphQL queries and view published content, as on a public site; the Learning access
        // handler must still keep lessons from people who are not enrolled.
        await InTenantAsync(services => RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "LearningTestRoles",
            ["steps"] = new JsonArray(
                // A front-end theme as on a real site: Orchard Core applies no placement without one.
                new JsonObject
                {
                    ["name"] = "feature",
                    ["enable"] = new JsonArray("CrestApps.OrchardCore.StorefrontTheme"),
                },
                new JsonObject
                {
                    ["name"] = "Themes",
                    ["Site"] = "CrestApps.OrchardCore.StorefrontTheme",
                },
                // The OpenID server with the token endpoint for client credentials (the LRS and the LTI services) and
                // the authorization endpoint, and token validation on this tenant for the Api scheme.
                new JsonObject
                {
                    ["name"] = "OpenIdServerSettings",
                    ["AccessTokenFormat"] = 1,
                    ["EnableTokenEndpoint"] = true,
                    ["EnableAuthorizationEndpoint"] = true,
                    ["AllowClientCredentialsFlow"] = true,
                    ["AllowImplicitFlow"] = true,
                },
                new JsonObject
                {
                    ["name"] = "OpenIdValidationSettings",
                    ["Tenant"] = ShellSettings.DefaultShellName,
                },
                new JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new JsonArray(
                        Role("Anonymous", "ViewContent", "ExecuteGraphQL"),
                        Role("Authenticated", "ViewContent", "ExecuteGraphQL"),
                        Role("CourseTeacher", "AccessAdminPanel", "ManageOwnCourses")),
                }),
        }));

        EnrolledUserId = await InTenantAsync(services => CreateUserAsync(services, "learner1"));
        await InTenantAsync(services => CreateUserAsync(services, "learner2"));
        SpareLearnerId = await InTenantAsync(services => CreateUserAsync(services, "learner3"));
        InstructorId = await InTenantAsync(services => CreateUserAsync(services, "instructor1", "CourseTeacher"));
        await InTenantAsync(CreateCourseAsync);
        EnrollmentId = await InTenantAsync(async services =>
        {
            var result = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = EnrolledUserId,
                CourseId = CourseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "fixture",
            });

            Assert.True(result.Succeeded, result.ErrorCode);

            return result.Enrollment.ItemId;
        });
    }

    /// <summary>
    /// Runs an operation in the tenant, with an HTTP context, as one of its background jobs would.
    /// </summary>
    public async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        var settings = ShellHost.TryGetSettings(ShellSettings.DefaultShellName, out var found) ? found : throw new InvalidOperationException("No Default tenant.");
        var result = default(T);

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

            try
            {
                result = await operation(services);
            }
            finally
            {
                accessor.HttpContext = null;
            }
        });

        return result;
    }

    public Task InTenantAsync(Func<IServiceProvider, Task> operation)
        => InTenantAsync<bool>(async services =>
        {
            await operation(services);

            return true;
        });

    /// <summary>
    /// Creates a browser-like client, signed in when a user name is given.
    /// </summary>
    public async Task<HttpClient> CreateClientAsync(string userName = null)
    {
        var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        {
            BaseAddress = BaseAddress,
        };

        if (userName is not null)
        {
            var login = await http.GetStringAsync("Login", TestContext.Current.CancellationToken);
            var signedIn = await http.PostAsync("Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["LoginForm.UserName"] = userName,
                ["LoginForm.Password"] = Password,
                ["__RequestVerificationToken"] = Token(login),
            }), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        }

        return http;
    }

    /// <summary>
    /// Reads the antiforgery token of a page.
    /// </summary>
    public static string Token(string html)
        => TokenPattern().Match(html) is { Success: true } match ? WebUtility.HtmlDecode(match.Groups["token"].Value) : throw new InvalidOperationException("The page has no antiforgery token.");

    /// <summary>
    /// Runs a recipe given as JSON.
    /// </summary>
    public static async Task RunRecipeAsync(IServiceProvider services, JsonObject recipe)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"crestapps-learning-recipe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "Recipe.json"), recipe.ToJsonString(), Encoding.UTF8);

            using var fileProvider = new PhysicalFileProvider(folder);
            var descriptor = new RecipeDescriptor
            {
                Name = recipe["name"]?.GetValue<string>() ?? "Recipe",
                FileProvider = fileProvider,
                BasePath = string.Empty,
                RecipeFileInfo = fileProvider.GetFileInfo("Recipe.json"),
            };

            await services.GetRequiredService<IRecipeExecutor>().ExecuteAsync(Guid.NewGuid().ToString("n"), descriptor, new Dictionary<string, object>(), CancellationToken.None);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }

        Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite")
            ?.GetMethod("ClearAllPools", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, null);

        for (var attempt = 0; attempt < 10 && Directory.Exists(_applicationDataPath); attempt++)
        {
            try
            {
                Directory.Delete(_applicationDataPath, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(200);
            }
        }
    }

    internal static JsonObject Role(string name, params string[] permissions)
        => new()
        {
            ["Name"] = name,
            ["Description"] = name + " role",
            ["PermissionBehavior"] = "Add",
            ["Permissions"] = new JsonArray(permissions.Select(permission => (JsonNode)permission).ToArray()),
        };

    internal static async Task<string> CreateUserAsync(IServiceProvider services, string userName, params string[] roles)
    {
        var errors = new List<string>();
        var user = await services.GetRequiredService<IUserService>().CreateUserAsync(new User
        {
            UserName = userName,
            Email = userName + "@example.com",
            EmailConfirmed = true,
            IsEnabled = true,
            RoleNames = roles.ToList(),
        }, Password, (_, message) => errors.Add(message));

        Assert.True(user is not null, string.Join(", ", errors));

        return ((User)user).UserId;
    }

    private async Task CreateCourseAsync(IServiceProvider services)
    {
        var contentManager = services.GetRequiredService<IContentManager>();

        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        course.DisplayText = "Workplace Safety";
        course.Alter<TitlePart>(part => part.Title = "Workplace Safety");
        course.Alter<CoursePart>(part =>
        {
            part.EnrollmentMode = EnrollmentMode.Open;
            part.Staff = [new CourseStaffMember { UserId = InstructorId, Role = LearningConstants.CourseStaffRoles.Instructor }];
        });
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        var page = await LessonAsync(contentManager, course, LearningConstants.ContentTypes.PageLesson, "Fire exits", SecretLessonText);
        var file = await LessonAsync(contentManager, course, LearningConstants.ContentTypes.FileLesson, "Evacuation guide", "The guide is attached.");

        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection
            {
                Id = "s1",
                Title = "Basics",
                Items =
                [
                    new CourseOutlineItem { ContentItemId = page.ContentItemId },
                    new CourseOutlineItem { ContentItemId = file.ContentItemId },
                ],
            },
        ]);
        await contentManager.UpdateAsync(course);
        await contentManager.PublishAsync(course);

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Evacuation guide for enrolled learners."));
        await mediaFileStore.CreateFileFromStreamAsync($"mediafields/{LearningConstants.ContentTypes.FileLesson}/{file.ContentItemId}/guide.txt", stream, overwrite: true);

        var other = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        other.DisplayText = "Forklift Basics";
        other.Alter<TitlePart>(part => part.Title = "Forklift Basics");
        await contentManager.CreateAsync(other, VersionOptions.Published);

        OtherCourseId = other.ContentItemId;
        CourseId = course.ContentItemId;
        PageLessonId = page.ContentItemId;
        FileLessonId = file.ContentItemId;
    }

    private static async Task<ContentItem> LessonAsync(IContentManager contentManager, ContentItem course, string contentType, string title, string body)
    {
        var lesson = await contentManager.NewAsync(contentType);
        lesson.DisplayText = title;
        lesson.Alter<TitlePart>(part => part.Title = title);
        lesson.Alter<HtmlBodyPart>(part => part.Html = $"<p>{body}</p>");
        lesson.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        lesson.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Viewed);
        await contentManager.CreateAsync(lesson, VersionOptions.Published);

        return lesson;
    }

    private async Task SetupAsync()
    {
        var settings = ShellHost.TryGetSettings(ShellSettings.DefaultShellName, out var found) ? found : null;
        var errors = new Dictionary<string, string>();

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

            var setup = services.GetRequiredService<ISetupService>();
            var recipe = (await setup.GetSetupRecipesAsync()).First(candidate => candidate.Name == "Blank");

            await setup.SetupAsync(new SetupContext
            {
                ShellSettings = settings,
                EnabledFeatures = [],
                Errors = errors,
                Recipe = recipe,
                Properties =
                {
                    [SetupConstants.SiteName] = "Learning tests",
                    [SetupConstants.AdminUsername] = "admin",
                    [SetupConstants.AdminEmail] = "admin@example.com",
                    [SetupConstants.AdminPassword] = Password,
                    [SetupConstants.DatabaseProvider] = DatabaseProviderValue.Sqlite,
                    [SetupConstants.DatabaseName] = "OrchardCore.db",
                    [SetupConstants.DatabaseTablePrefix] = string.Empty,
                },
            });

            accessor.HttpContext = null;
        });

        Assert.Empty(errors);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex TokenPattern();
}

[CollectionDefinition(Name)]
public sealed class LearningSiteCollection : ICollectionFixture<LearningSiteFixture>
{
    public const string Name = "Learning site";
}
