using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class TenantOnboardingSubscriptionHandler : SubscriptionHandlerBase
{
    private readonly IShellHost _shellHost;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ISetupService _setupService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantOnboardingSubscriptionHandler> _logger;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public TenantOnboardingSubscriptionHandler(
        IShellHost shellHost,
        IShellSettingsManager shellSettingsManager,
        ShellSettings shellSettings,
        IClock clock,
        ISetupService setupService,
        IServiceProvider serviceProvider,
        ILogger<TenantOnboardingSubscriptionHandler> logger,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _shellHost = shellHost;
        _shellSettingsManager = shellSettingsManager;
        _shellSettings = shellSettings;
        _clock = clock;
        _setupService = setupService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    public override Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
        {
            return Task.CompletedTask;
        }

        if (!context.SubscriptionContentItem.TryGet<TenantOnboardingPart>(out var tenantOnboardingPart))
        {
            return Task.CompletedTask;
        }

        var step = new SubscriptionFlowStep()
        {
            Title = S["New Site Info"],
            Description = S["Information to be used for setting up your new site."],
            Key = SubscriptionConstants.StepKey.TenantOnboarding,
            CollectData = true,
            Plan = new SubscriptionPlan()
            {
                Description = context.SubscriptionContentItem.DisplayText,
                Id = context.Session.ContentItemVersionId,
                InitialAmount = subscriptionPart.InitialAmount,
                BillingAmount = subscriptionPart.BillingAmount,
                SubscriptionDayDelay = subscriptionPart.SubscriptionDayDelay,
                BillingDuration = subscriptionPart.BillingDuration,
                DurationType = subscriptionPart.DurationType,
                BillingCycleLimit = subscriptionPart.BillingCycleLimit,
            },
            Order = 100,
        };

        step.Data.TryAdd("RecipeName", tenantOnboardingPart.RecipeName);
        step.Data.TryAdd("FeatureProfile", tenantOnboardingPart.FeatureProfile);

        context.Session.Steps.Add(step);

        return Task.CompletedTask;
    }

    public override Task CompletingAsync(SubscriptionFlowCompletingContext context)
    {
        if (!context.Flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            throw new InvalidOperationException("Unable to local the new site info.");
        }

        var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        if (_shellHost.TryGetSettings(info.TenantName, out _))
        {
            throw new InvalidOperationException("Tenant name is unavailable.");
        }

        var shellSettings = _shellHost.GetAllSettings();

        var domains = info.GetDomains();

        if (shellSettings.Any(settings => settings.HasUrlHost(domains)))
        {
            throw new InvalidOperationException("Provided domain belong to another tenant.");
        }

        if (!string.IsNullOrEmpty(info.Prefix) && shellSettings.Any(settings => settings.HasUrlPrefix(info.Prefix)))
        {
            throw new InvalidOperationException("Provided prefix belong to another tenant.");
        }

        return Task.CompletedTask;
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        if (!context.Flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            return;
        }

        var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        using var shellSettings = await CreateTenantAsync(info);

        RecipeDescriptor recipeDescriptor = null;

        if (string.IsNullOrEmpty(info.RecipeName))
        {
            var tempFilename = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilename, info.RecipeName);

            var fileProvider = new PhysicalFileProvider(Path.GetDirectoryName(tempFilename));

            recipeDescriptor = new RecipeDescriptor
            {
                FileProvider = fileProvider,
                BasePath = string.Empty,
                RecipeFileInfo = fileProvider.GetFileInfo(Path.GetFileName(tempFilename))
            };
        }

        var setupContext = new SetupContext
        {
            ShellSettings = shellSettings,
            EnabledFeatures = [],
            Errors = new Dictionary<string, string>(),
            Recipe = recipeDescriptor,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, shellSettings.Name },
                { SetupConstants.AdminUsername, info.AdminUsername },
                { SetupConstants.AdminEmail, info.AdminEmail },
                { SetupConstants.AdminPassword, info.AdminPassword},
                { SetupConstants.SiteTimeZone, _clock.GetSystemTimeZone() },
                { SetupConstants.DatabaseProvider, shellSettings["DatabaseProvider"] },
                { SetupConstants.DatabaseConnectionString, shellSettings["ConnectionString"] },
                { SetupConstants.DatabaseTablePrefix, shellSettings["TablePrefix"] },
                { SetupConstants.DatabaseSchema, shellSettings["Schema"] },
            }
        };

        await _setupService.SetupAsync(setupContext);

        // Lazily resolve the workflow manager as it may not be registered.
        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        // Check if a component in the Setup failed.
        if (setupContext.Errors.Count > 0)
        {
            _logger.LogError("Unable to auto setup a new subscribed tenant. Errors: {Errors}", string.Join(';', setupContext.Errors));

            if (workflowManager != null)
            {
                var input = new Dictionary<string, object>
                {
                    { "TenantName", shellSettings.Name },
                    { "Errors", setupContext.Errors },
                };

                await workflowManager.TriggerEventAsync(SubscribedTenantFailedSetupEvent.EventName, input, correlationId: $"TenantAutoSetup_{shellSettings.Name}");
            }
        }
        else
        {
            _logger.LogInformation("New subscribed tenant was auto setup successfully.");

            if (workflowManager != null)
            {
                var input = new Dictionary<string, object>
                {
                    { "TenantName", shellSettings.Name },
                };

                await workflowManager.TriggerEventAsync(SubscribedTenantSetupSucceededEvent.EventName, input, correlationId: $"TenantAutoSetup_{shellSettings.Name}");
            }
        }
    }

    private async Task<ShellSettings> CreateTenantAsync(TenantOnboardingStep info)
    {
        // Creates a default shell settings based on the configuration.
        var shellSettings = _shellSettingsManager
            .CreateDefaultSettings()
            .AsUninitialized()
            .AsDisposable();

        // Create the new tenant.
        shellSettings.Name = info.TenantName;
        shellSettings.RequestUrlHost = string.Join(ShellSettings.HostSeparators.First(), info.GetDomains());
        shellSettings.RequestUrlPrefix = info.Prefix;

        shellSettings["Description"] = info.TenantTitle;
        shellSettings["Secret"] = Guid.NewGuid().ToString();
        shellSettings["RecipeName"] = info.RecipeName;
        shellSettings["FeatureProfile"] = info.FeatureProfile;

        shellSettings["TablePrefix"] = info.TenantName;
        shellSettings["Schema"] = _shellSettings["Schema"];
        shellSettings["ConnectionString"] = _shellSettings["ConnectionString"];
        shellSettings["DatabaseProvider"] = _shellSettings["DatabaseProvider"];

        await _shellHost.UpdateShellSettingsAsync(shellSettings);

        return shellSettings;
    }
}
