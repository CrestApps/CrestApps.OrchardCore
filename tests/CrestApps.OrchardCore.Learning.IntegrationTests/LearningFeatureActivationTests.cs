using CrestApps.OrchardCore.Learning.Commerce;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Abstractions.Setup;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Data.Migration.Records;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Setup.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Turning on every Learning feature in one go on a new online store, as from the Features page, runs the migration of
/// every feature in that same activation, so Learning Commerce's tables are there without restarting the site. Orchard
/// Core stops running the migrations of a feature for the rest of an activation when one of them returns 0, so the
/// learning path products migration, which waits for the Learning Path type, used to stop Learning Commerce's own
/// migration whenever it happened to run first.
/// </summary>
public sealed class LearningFeatureActivationTests
{
    private static readonly string[] _learningFeatures =
    [
        "CrestApps.OrchardCore.Learning",
        "CrestApps.OrchardCore.Learning.AI",
        "CrestApps.OrchardCore.Learning.AI.Authoring",
        "CrestApps.OrchardCore.Learning.Assessments",
        "CrestApps.OrchardCore.Learning.Assessments.Gradebook",
        "CrestApps.OrchardCore.Learning.Certifications",
        "CrestApps.OrchardCore.Learning.Classes",
        "CrestApps.OrchardCore.Learning.Commerce",
        "CrestApps.OrchardCore.Learning.Coursework",
        "CrestApps.OrchardCore.Learning.Credentials",
        "CrestApps.OrchardCore.Learning.LearnerPages",
        "CrestApps.OrchardCore.Learning.Lti",
        "CrestApps.OrchardCore.Learning.Packages.Scorm",
        "CrestApps.OrchardCore.Learning.Programs",
        "CrestApps.OrchardCore.Learning.Programs.RequiredLearning",
        "CrestApps.OrchardCore.Learning.Programs.TeamLearning",
        "CrestApps.OrchardCore.Learning.SelfEnrollment",
        "CrestApps.OrchardCore.Learning.Xapi",
    ];

    [Fact]
    public async Task EnablingEveryLearningFeatureAtOnce_OnANewOnlineStore_RunsLearningCommercesMigration_WithoutARestart()
    {
        // Arrange
        await using var host = await Host.StartAsync();
        await host.SetupAsync("OnlineStore");

        // Act: one bulk enable, as the Features page does.
        await host.InTenantAsync(async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var available = await features.GetAvailableFeaturesAsync();

            await features.EnableFeaturesAsync(available.Where(feature => _learningFeatures.Contains(feature.Id)), force: true);

            return true;
        });

        // Assert: the next scope is the new shell, whose activation ran the migrations.
        var (versions, owners) = await host.InTenantAsync<(Dictionary<string, int?> Versions, string Owners)>(async services =>
        {
            var record = await services.GetRequiredService<global::YesSql.ISession>().Query<DataMigrationRecord>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            var migrations = services.GetServices<IDataMigration>().Where(migration => migration.GetType().Namespace == "CrestApps.OrchardCore.Learning.Commerce.Migrations").ToArray();
            var typeFeatures = services.GetRequiredService<ITypeFeatureProvider>();

            return (
                record.DataMigrations.Where(migration => migration.DataMigrationClass.StartsWith("CrestApps.OrchardCore.Learning.Commerce.", StringComparison.Ordinal)).ToDictionary(migration => migration.DataMigrationClass.Split('.')[^1], migration => migration.Version),
                string.Join("; ", migrations.Select(migration => migration.GetType().Name + " -> " + string.Join(", ", typeFeatures.GetFeaturesForDependency(migration.GetType()).Select(feature => feature.Id)))));
        });

        Assert.True(versions.TryGetValue("LearningCommerceMigrations", out var version) && version > 0, $"Learning Commerce's migration did not run. Recorded: {string.Join(", ", versions.Select(pair => pair.Key + "=" + pair.Value))}. Owners: {owners}");

        var pools = await host.InTenantAsync(services => services.GetRequiredService<global::YesSql.ISession>().QueryIndex<global::CrestApps.OrchardCore.Learning.Commerce.Core.Indexes.LicensePoolIndex>(LearningCommerceConstants.CollectionName).CountAsync());
        Assert.Equal(0, pools);

        // Learning paths are sold on their pages in the same activation too, not after the next restart.
        Assert.Equal(1, versions.GetValueOrDefault("LearningPathProductMigrations"));
        var pathSold = await host.InTenantAsync(async services => (await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(global::CrestApps.OrchardCore.Learning.Programs.ProgramsConstants.LearningPathContentType))?.Parts.Any(part => part.Name == "LearningProductPart") == true);
        Assert.True(pathSold);
    }

    private sealed class Host : IAsyncDisposable
    {
        private WebApplication _application;
        private string _applicationDataPath;

        private IShellHost ShellHost { get; set; }

        public static async Task<Host> StartAsync()
        {
            var host = new Host { _applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-learning-activation-{Guid.NewGuid():N}") };
            var webRootPath = Path.Combine(host._applicationDataPath, "wwwroot");
            Directory.CreateDirectory(webRootPath);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(LearningFeatureActivationTests).Assembly.FullName,
                ContentRootPath = host._applicationDataPath,
                EnvironmentName = Environments.Development,
                WebRootPath = webRootPath,
            });

            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["OrchardCore:OrchardCore_Documents:CheckConcurrency"] = bool.FalseString;
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Services.AddOrchardCms();
            builder.Services.Configure<ShellOptions>(options => options.ShellsApplicationDataPath = host._applicationDataPath);

            host._application = builder.Build();
            host._application.UseOrchardCore();
            await host._application.StartAsync();

            host.ShellHost = host._application.Services.GetRequiredService<IShellHost>();
            await host.ShellHost.InitializeAsync();

            return host;
        }

        public async Task SetupAsync(string recipeName)
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
                var recipe = (await setup.GetSetupRecipesAsync()).First(candidate => candidate.Name == recipeName);

                await setup.SetupAsync(new SetupContext
                {
                    ShellSettings = settings,
                    EnabledFeatures = [],
                    Errors = errors,
                    Recipe = recipe,
                    Properties =
                    {
                        [SetupConstants.SiteName] = "Learning activation",
                        [SetupConstants.AdminUsername] = "admin",
                        [SetupConstants.AdminEmail] = "admin@example.com",
                        [SetupConstants.AdminPassword] = LearningSiteFixture.Password,
                        [SetupConstants.DatabaseProvider] = DatabaseProviderValue.Sqlite,
                        [SetupConstants.DatabaseName] = "OrchardCore.db",
                        [SetupConstants.DatabaseTablePrefix] = string.Empty,
                        [SetupConstants.SiteTimeZone] = "America/Los_Angeles",
                    },
                });

                accessor.HttpContext = null;
            });

            Assert.Empty(errors);
        }

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
    }
}
