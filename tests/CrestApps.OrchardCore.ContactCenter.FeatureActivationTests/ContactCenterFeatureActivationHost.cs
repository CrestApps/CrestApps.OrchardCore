using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchardCore.Abstractions.Setup;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Setup.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterFeatureActivationHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly string _applicationDataPath;
    private readonly IShellHost _shellHost;
    private readonly IShellSettingsManager _shellSettingsManager;

    private ContactCenterFeatureActivationHost(
        WebApplication application,
        string applicationDataPath)
    {
        _application = application;
        _applicationDataPath = applicationDataPath;
        _shellHost = application.Services.GetRequiredService<IShellHost>();
        _shellSettingsManager = application.Services.GetRequiredService<IShellSettingsManager>();
    }

    public static async Task<ContactCenterFeatureActivationHost> StartAsync()
    {
        var applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-contact-center-{Guid.NewGuid():N}");
        var webRootPath = Path.Combine(applicationDataPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ContactCenterFeatureActivationHost).Assembly.FullName,
            ContentRootPath = applicationDataPath,
            EnvironmentName = Environments.Development,
            WebRootPath = webRootPath,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services
            .AddOrchardCms();
        builder.Services.Configure<ShellOptions>(options => options.ShellsApplicationDataPath = applicationDataPath);
        builder.Configuration["OrchardCore:OrchardCore_Documents:CheckConcurrency"] = bool.FalseString;

        var application = builder.Build();
        application.UseOrchardCore();
        await application.StartAsync();
        await application.Services.GetRequiredService<IShellHost>().InitializeAsync();

        return new ContactCenterFeatureActivationHost(application, applicationDataPath);
    }

    public async Task<ContactCenterTenant> CreateTenantAsync(ContactCenterTenantProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var tenantName = $"cc{Guid.NewGuid():N}";
        var settings = _shellSettingsManager.CreateDefaultSettings();
        settings.Name = tenantName;
        settings.RequestUrlPrefix = tenantName;
        settings.State = TenantState.Uninitialized;

        await _shellHost.UpdateShellSettingsAsync(settings);
        await SetupTenantAsync(settings);
        await EnableProfileAsync(settings, profile);

        return new ContactCenterTenant(settings, profile);
    }

    public async Task AssertTenantAsync(ContactCenterTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        await using var scope = await _shellHost.GetScopeAsync(tenant.Settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var featureManager = services.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await featureManager.GetAvailableFeaturesAsync();
            var enabledFeatures = await featureManager.GetEnabledFeaturesAsync();
            var expectedFeatures = GetDependencyClosure(availableFeatures, tenant.Profile.Features);
            var enabledFeatureIds = enabledFeatures.Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
            var expectedCrestAppsFeatures = expectedFeatures
                .Where(IsCrestAppsFeature)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var enabledCrestAppsFeatures = enabledFeatureIds
                .Where(IsCrestAppsFeature)
                .Where(featureId => !featureId.StartsWith(
                    typeof(ContactCenterFeatureActivationHost).Assembly.GetName().Name ?? string.Empty,
                    StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.All(expectedFeatures, featureId => Assert.Contains(featureId, enabledFeatureIds));
            Assert.Equal(expectedCrestAppsFeatures, enabledCrestAppsFeatures);
            Assert.NotNull(services.GetRequiredService<IAgentPresenceManager>());
            Assert.NotNull(services.GetRequiredService<IAgentAvailabilityService>());
            Assert.NotNull(services.GetRequiredService<IAgentAvailabilityRecoveryService>());
            Assert.NotNull(services.GetRequiredService<IActivityQueueService>());
            Assert.NotNull(services.GetRequiredService<IActivityRoutingService>());
            Assert.NotNull(services.GetRequiredService<IInteractionManager>());
            Assert.NotEmpty(services.GetServices<IBackgroundTask>());
            Assert.Single(services.GetServices<IBackgroundTask>().OfType<AgentAvailabilityRecoveryBackgroundTask>());

            var voiceProviders = services.GetServices<IContactCenterVoiceProvider>();
            var provider = Assert.Single(voiceProviders);
            Assert.Equal(GetExpectedProviderName(tenant.Profile), provider.TechnicalName);
        });
    }

    public async Task AssertVoiceFeatureAsync(ContactCenterTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        await using var scope = await _shellHost.GetScopeAsync(tenant.Settings);
        await scope.UsingAsync(shellScope =>
        {
            var services = shellScope.ServiceProvider;

            Assert.NotNull(services.GetRequiredService<IProviderCommandManager>());
            Assert.NotNull(services.GetRequiredService<IProviderCommandStateService>());
            Assert.NotNull(services.GetRequiredService<IProviderCommandProcessor>());
            Assert.Single(services.GetServices<IBackgroundTask>().OfType<ProviderCommandRecoveryBackgroundTask>());
            Assert.Single(services.GetServices<IIndexProvider>().OfType<ProviderCommandIndexProvider>());
            Assert.Single(
                services.GetServices<IDataMigration>(),
                migration => migration.GetType().Name == "ProviderCommandIndexMigrations");

            return Task.CompletedTask;
        });
    }

    public async Task DisableAndReenableProviderAsync(ContactCenterTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var providerFeature = GetProviderFeature(tenant.Profile);
        await UpdateFeatureAsync(tenant, providerFeature, enable: false);
        await using (var disabledScope = await _shellHost.GetScopeAsync(tenant.Settings))
        {
            await disabledScope.UsingAsync(shellScope =>
            {
                Assert.Empty(shellScope.ServiceProvider.GetServices<IContactCenterVoiceProvider>());

                return Task.CompletedTask;
            });
        }

        await UpdateFeatureAsync(tenant, providerFeature, enable: true);
        await AssertTenantAsync(tenant);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();

        if (Directory.Exists(_applicationDataPath))
        {
            Directory.Delete(_applicationDataPath, recursive: true);
        }
    }

    private async Task SetupTenantAsync(ShellSettings settings)
    {
        await using var scope = await _shellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
            };

            var setupService = services.GetRequiredService<ISetupService>();
            var recipes = await setupService.GetSetupRecipesAsync();
            var recipe = recipes.Single(recipe => recipe.Name == "Blank");
            var errors = new Dictionary<string, string>();
            var setupContext = new SetupContext
            {
                ShellSettings = settings,
                EnabledFeatures = [],
                Errors = errors,
                Recipe = recipe,
                Properties =
                {
                    [SetupConstants.SiteName] = settings.Name,
                    [SetupConstants.AdminUsername] = "admin",
                    [SetupConstants.AdminEmail] = $"{settings.Name}@example.invalid",
                    [SetupConstants.AdminPassword] = $"Test-{Guid.NewGuid():N}!aA1",
                    [SetupConstants.DatabaseProvider] = DatabaseProviderValue.Sqlite,
                    [SetupConstants.DatabaseName] = $"{settings.Name}.db",
                    [SetupConstants.DatabaseTablePrefix] = settings.Name,
                },
            };

            await setupService.SetupAsync(setupContext);

            Assert.Empty(errors);
            httpContextAccessor.HttpContext = null;
        });
    }

    private async Task EnableProfileAsync(
        ShellSettings settings,
        ContactCenterTenantProfile profile)
    {
        await using var scope = await _shellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var featureManager = shellScope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await featureManager.GetAvailableFeaturesAsync();
            var featuresById = availableFeatures.ToDictionary(feature => feature.Id, StringComparer.Ordinal);
            var missingFeatures = profile.Features.Where(featureId => !featuresById.ContainsKey(featureId)).ToArray();

            Assert.Empty(missingFeatures);
            await featureManager.EnableFeaturesAsync(
                profile.Features.Select(featureId => featuresById[featureId]),
                force: true);
        });
    }

    private async Task UpdateFeatureAsync(
        ContactCenterTenant tenant,
        string featureId,
        bool enable)
    {
        await using var scope = await _shellHost.GetScopeAsync(tenant.Settings);
        await scope.UsingAsync(async shellScope =>
        {
            var featureManager = shellScope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = await featureManager.GetAvailableFeaturesAsync();
            var feature = availableFeatures.Single(candidate => candidate.Id == featureId);

            if (enable)
            {
                await featureManager.EnableFeaturesAsync([feature], force: true);
            }
            else
            {
                await featureManager.DisableFeaturesAsync([feature], force: false);
            }
        });
    }

    private static HashSet<string> GetDependencyClosure(
        IEnumerable<IFeatureInfo> availableFeatures,
        IEnumerable<string> seedFeatures)
    {
        var featuresById = availableFeatures.ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(seedFeatures);

        while (pending.TryPop(out var featureId))
        {
            if (!closure.Add(featureId))
            {
                continue;
            }

            Assert.True(featuresById.TryGetValue(featureId, out var feature), $"Feature '{featureId}' is not available.");

            foreach (var dependency in feature.Dependencies)
            {
                pending.Push(dependency);
            }
        }

        return closure;
    }

    private static string GetProviderFeature(ContactCenterTenantProfile profile)
    {
        return profile.Features.Single(featureId =>
            featureId.EndsWith(".ContactCenterVoice", StringComparison.Ordinal) &&
            !featureId.StartsWith("CrestApps.OrchardCore.ContactCenter.", StringComparison.Ordinal));
    }

    private static string GetExpectedProviderName(ContactCenterTenantProfile profile)
    {
        return profile.ProviderProfile.StartsWith("asterisk-", StringComparison.Ordinal)
            ? "Asterisk"
            : "DialPad";
    }

    private static bool IsCrestAppsFeature(string featureId)
    {
        return featureId.StartsWith("CrestApps.", StringComparison.Ordinal);
    }
}
