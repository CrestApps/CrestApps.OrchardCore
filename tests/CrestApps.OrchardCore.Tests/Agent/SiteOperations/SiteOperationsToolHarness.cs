using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Localization;
using OrchardCore.Localization.Data;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;
using OrchardCore.UrlRewriting;
using OrchardCore.UrlRewriting.Handlers;
using OrchardCore.UrlRewriting.Models;
using OrchardCore.UrlRewriting.Services;

namespace CrestApps.OrchardCore.Tests.Agent.SiteOperations;

/// <summary>
/// Builds the services of the site operations tools on the real Orchard Core services where they run without a
/// database (the URL rewriting rule manager and its handlers, the dynamic translation resource manager, the health
/// check service) and on recording doubles elsewhere (the background service monitor, the recipe step that saves
/// translations, the tenant release).
/// </summary>
internal sealed class SiteOperationsToolHarness
{
    /// <summary>
    /// The ID of the redirect rule the store starts with.
    /// </summary>
    public const string RedirectRuleId = "rule-1";

    /// <summary>
    /// Gets or sets whether a user is signed in.
    /// </summary>
    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants or refuses a permission. The default grants everything.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; }

    /// <summary>
    /// Gets or sets whether the background service, and so its monitor, is registered.
    /// </summary>
    public bool HasMonitor { get; set; } = true;

    /// <summary>
    /// Gets the rules of the in-memory rule store, by ID.
    /// </summary>
    public Dictionary<string, RewriteRule> Rules { get; } = [];

    /// <summary>
    /// Gets the number of times the tenant release was requested.
    /// </summary>
    public int Releases { get; private set; }

    /// <summary>
    /// Gets the background tasks whose run was requested, by technical name.
    /// </summary>
    public List<string> RunRequests { get; } = [];

    /// <summary>
    /// Gets the recipe steps the tools ran.
    /// </summary>
    public List<RecipeExecutionContext> RecipeSteps { get; } = [];

    /// <summary>
    /// Gets the saved dynamic translations, by culture and then by context and key.
    /// </summary>
    public Dictionary<string, Dictionary<(string Context, string Key), string>> Translations { get; } = new(StringComparer.Ordinal)
    {
        ["fr-FR"] = new()
        {
            [("Content Types", "Blog Post")] = "Article de blog",
        },
    };

    /// <summary>
    /// Builds the services.
    /// </summary>
    /// <returns>The service provider.</returns>
    public ServiceProvider Build()
    {
        Rules[RedirectRuleId] = CreateRedirectRule();

        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        services.AddLogging();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(new ShellSettings { Name = ShellSettings.DefaultShellName });
        services.AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc)));

        var releaseManager = new Mock<IShellReleaseManager>();
        releaseManager.Setup(manager => manager.RequestRelease()).Callback(() => Releases++);
        services.AddSingleton(releaseManager.Object);

        // Background tasks.
        services.AddSingleton<IBackgroundTask, IndexingTestBackgroundTask>();
        services.AddSingleton<IBackgroundTask, CleanupTestBackgroundTask>();
        services.AddSingleton<IBackgroundTaskSettingsProvider>(new TestBackgroundTaskSettingsProvider());

        if (HasMonitor)
        {
            services.AddSingleton<IBackgroundTaskMonitor>(new TestBackgroundTaskMonitor(this));
        }

        // URL rewriting, on the real rule manager, handlers and sources.
        services.AddSingleton<IRewriteRulesStore>(new InMemoryRewriteRulesStore(Rules));
        services.AddScoped<IRewriteRulesManager, RewriteRulesManager>();
        services.AddScoped<IRewriteRuleHandler, RewriteRuleHandler>();
        services.AddScoped<IRewriteRuleHandler, UrlRedirectRuleHandler>();
        services.AddScoped<IRewriteRuleHandler, UrlRewriteRuleHandler>();
        services.AddRewriteRuleSource<UrlRedirectRuleSource>(UrlRedirectRuleSource.SourceName);
        services.AddRewriteRuleSource<UrlRewriteRuleSource>(UrlRewriteRuleSource.SourceName);

        // Health checks.
        services.AddHealthChecks()
            .AddCheck("Database", () => HealthCheckResult.Healthy("The database answered."))
            .AddCheck("Redis", () => HealthCheckResult.Unhealthy("Couldn't connect to the Redis server.", new InvalidOperationException("password=secret")));

        // Dynamic translations.
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetSupportedCulturesAsync()).ReturnsAsync(["en-US", "fr-FR"]);
        services.AddSingleton(localizationService.Object);
        services.AddSingleton<ILocalizationDataProvider>(new TestLocalizationDataProvider());
        services.AddSingleton<IDataTranslationProvider>(new TestDataTranslationProvider(this));
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddTransient<DataResourceManager>();
        services.AddSingleton<IRecipeStepHandler>(new RecordingRecipeStepHandler(this));

        return services.BuildServiceProvider();
    }

    private static RewriteRule CreateRedirectRule()
    {
        var rule = new RewriteRule
        {
            Id = RedirectRuleId,
            Name = "Old blog",
            Source = UrlRedirectRuleSource.SourceName,
            Order = 0,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        rule.Put(new UrlRedirectSourceMetadata
        {
            Pattern = "^old-blog/(.*)$",
            SubstitutionPattern = "/blog/$1",
            IsCaseInsensitive = true,
            QueryStringPolicy = QueryStringPolicy.Append,
            RedirectType = RedirectType.MovedPermanently,
        });

        return rule;
    }

    /// <summary>
    /// A task that declares its settings with the attribute.
    /// </summary>
    [BackgroundTask(Title = "Index Update", Schedule = "*/5 * * * *", Description = "Updates the search indexes.")]
    public sealed class IndexingTestBackgroundTask : IBackgroundTask
    {
        public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>
    /// A task that declares no settings, so it gets the defaults.
    /// </summary>
    public sealed class CleanupTestBackgroundTask : IBackgroundTask
    {
        public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TestBackgroundTaskSettingsProvider : IBackgroundTaskSettingsProvider
    {
        public IChangeToken ChangeToken { get; } = new CancellationChangeToken(CancellationToken.None);

        // The cleanup task was disabled on the admin page; the index task runs with its defaults.
        public Task<BackgroundTaskSettings> GetSettingsAsync(IBackgroundTask task)
            => Task.FromResult(task is CleanupTestBackgroundTask
                ? new BackgroundTaskSettings
                {
                    Name = typeof(CleanupTestBackgroundTask).FullName,
                    Title = "Cleanup Test",
                    Enable = false,
                    Schedule = "0 2 * * *",
                }
                : null);
    }

    private sealed class TestBackgroundTaskMonitor : IBackgroundTaskMonitor
    {
        private readonly SiteOperationsToolHarness _harness;

        public TestBackgroundTaskMonitor(SiteOperationsToolHarness harness)
        {
            _harness = harness;
        }

        public Task<IReadOnlyList<BackgroundTaskState>> GetStatesAsync(string tenant)
            => Task.FromResult<IReadOnlyList<BackgroundTaskState>>([CreateIndexingState(tenant)]);

        public Task<BackgroundTaskState> GetStateAsync(string tenant, string name)
            => Task.FromResult(name == typeof(IndexingTestBackgroundTask).FullName ? CreateIndexingState(tenant) : null);

        public Task<BackgroundTaskRunRequestResult> RequestRunAsync(string tenant, string name)
        {
            _harness.RunRequests.Add(name);

            return Task.FromResult(name == typeof(IndexingTestBackgroundTask).FullName
                ? BackgroundTaskRunRequestResult.Queued
                : BackgroundTaskRunRequestResult.NotFound);
        }

        private static BackgroundTaskState CreateIndexingState(string tenant)
        {
            var failedRun = new BackgroundTaskRun
            {
                StartTime = new DateTime(2026, 11, 17, 9, 55, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 11, 17, 9, 55, 2, DateTimeKind.Utc),
                Trigger = BackgroundTaskTrigger.Schedule,
                Result = BackgroundTaskRunResult.Failed,
                ErrorMessage = "The index is locked.",
                ErrorDetails = "System.InvalidOperationException: The index is locked. at Somewhere()",
            };

            return new BackgroundTaskState
            {
                Name = typeof(IndexingTestBackgroundTask).FullName,
                Tenant = tenant,
                Status = BackgroundTaskStatus.Idle,
                LastStartTime = failedRun.StartTime,
                NextStartTime = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc),
                LastRun = failedRun,
                RunCount = 12,
                FailureCount = 1,
                RecentRuns = [failedRun],
            };
        }
    }

    private sealed class InMemoryRewriteRulesStore : IRewriteRulesStore
    {
        private readonly Dictionary<string, RewriteRule> _rules;

        public InMemoryRewriteRulesStore(Dictionary<string, RewriteRule> rules)
        {
            _rules = rules;
        }

        public Task DeleteAsync(RewriteRule rule)
        {
            _rules.Remove(rule.Id);

            return Task.CompletedTask;
        }

        public Task<RewriteRule> FindByIdAsync(string id)
            => Task.FromResult(_rules.GetValueOrDefault(id));

        public Task<IEnumerable<RewriteRule>> GetAllAsync()
            => Task.FromResult<IEnumerable<RewriteRule>>(_rules.Values.ToList());

        public Task SaveAsync(RewriteRule rule)
        {
            _rules[rule.Id] = rule;

            return UpdateOrderAndSaveAsync(_rules.Values.ToList());
        }

        public Task<IEnumerable<RewriteRule>> UpdateOrderAndSaveAsync(IEnumerable<RewriteRule> rules)
        {
            var order = 0;

            foreach (var rule in rules)
            {
                rule.Order = order++;
            }

            return Task.FromResult<IEnumerable<RewriteRule>>(_rules.Values.ToList());
        }
    }

    private sealed class TestLocalizationDataProvider : ILocalizationDataProvider
    {
        public Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
            => Task.FromResult<IEnumerable<DataLocalizedString>>(
            [
                new("Content Types", "Blog Post", string.Empty),
                new("Content Types", "Article", string.Empty),
                new("Permissions", "Manage blogs", string.Empty),
            ]);
    }

    private sealed class TestDataTranslationProvider : IDataTranslationProvider
    {
        private readonly SiteOperationsToolHarness _harness;

        public TestDataTranslationProvider(SiteOperationsToolHarness harness)
        {
            _harness = harness;
        }

        public void LoadTranslations(string cultureName, CultureDictionary dictionary)
        {
            if (_harness.Translations.TryGetValue(cultureName, out var translations))
            {
                dictionary.MergeTranslations(translations.Select(translation => new CultureDictionaryRecord(translation.Key.Key, translation.Key.Context, [translation.Value])));
            }
        }
    }

    private sealed class RecordingRecipeStepHandler : IRecipeStepHandler
    {
        private readonly SiteOperationsToolHarness _harness;

        public RecordingRecipeStepHandler(SiteOperationsToolHarness harness)
        {
            _harness = harness;
        }

        public Task ExecuteAsync(RecipeExecutionContext context)
        {
            _harness.RecipeSteps.Add(context);

            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
