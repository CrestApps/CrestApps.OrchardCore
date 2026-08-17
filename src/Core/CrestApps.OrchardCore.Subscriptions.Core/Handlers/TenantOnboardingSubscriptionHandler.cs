using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds tenant onboarding to subscription flows and provisions a subscribed tenant after successful payment.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOnboardingSubscriptionHandler"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host used to inspect and update tenant shell settings.</param>
    /// <param name="shellSettingsManager">The manager used to create tenant shell settings.</param>
    /// <param name="shellSettings">The shell settings of the current tenant.</param>
    /// <param name="clock">The clock used to set setup time zone information.</param>
    /// <param name="setupService">The setup service used to read recipes and initialize the new tenant.</param>
    /// <param name="serviceProvider">The service provider used to resolve optional workflow services.</param>
    /// <param name="logger">The logger used to record tenant provisioning results.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used for persisted onboarding step data.</param>
    /// <param name="stringLocalizer">The localizer used for subscription flow step text.</param>
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

    /// <summary>
    /// Adds a tenant onboarding step when the subscription content item includes tenant onboarding settings.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is being activated.</param>
    public override Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out _) ||
            !context.SubscriptionContentItem.TryGet<ProductPart>(out _))
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
            Order = 100,
        };

        step.Data.TryAdd("RecipeName", tenantOnboardingPart.RecipeName);
        step.Data.TryAdd("FeatureProfile", tenantOnboardingPart.FeatureProfile);

        context.Session.Steps.Add(step);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that the requested tenant name, domains, prefix, and setup recipe are available before completion.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is completing.</param>
    public override async Task CompletingAsync(SubscriptionFlowCompletingContext context)
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

        if (domains.Length > 0 && shellSettings.Any(settings => settings.HasUrlHost(domains)))
        {
            throw new InvalidOperationException("Provided domain belong to another tenant.");
        }

        if (!string.IsNullOrEmpty(info.Prefix) && shellSettings.Any(settings => settings.HasUrlPrefix(info.Prefix)))
        {
            throw new InvalidOperationException("Provided prefix belong to another tenant.");
        }

        var recipes = await _setupService.GetSetupRecipesAsync();

        if (!recipes.Any())
        {
            throw new InvalidOperationException("No startup recipes found!");
        }
    }

    /// <summary>
    /// Provisions the tenant requested during onboarding and raises workflow events for the setup result.
    /// </summary>
    /// <param name="context">The context for the subscription flow that completed.</param>
    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        if (!context.Flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            return;
        }

        var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        // Lazily resolve the workflow manager as it may not be registered.
        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        // Payment has already succeeded by the time we reach this point. Any failure while
        // provisioning the tenant must be captured and surfaced through a durable workflow event
        // (rather than thrown) so operators can remediate (retry, refund, contact the customer)
        // without the subscriber being left with a charge and no site.
        var tenantName = info?.TenantName;

        // Only provisioning is wrapped in the try/catch. The success/failure workflow event is then
        // dispatched afterwards so a fault while raising one event can never be mistaken for a
        // provisioning fault, nor cause a second (escaping) event dispatch.
        IDictionary<string, string> failureErrors = null;

        try
        {
            var recipes = await _setupService.GetSetupRecipesAsync();
            using var shellSettings = await CreateTenantAsync(info);
            tenantName = shellSettings.Name;

            var setupContext = new SetupContext
            {
                ShellSettings = shellSettings,
                EnabledFeatures = [],
                Errors = new Dictionary<string, string>(),
                Recipe = recipes.FirstOrDefault(x => x.Name == info.RecipeName) ?? recipes.First(),
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

            if (setupContext.Errors.Count > 0)
            {
                failureErrors = setupContext.Errors;
            }
        }
        catch (OperationCanceledException)
        {
            // Preserve cancellation semantics rather than reporting a provisioning failure.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while provisioning the subscribed tenant '{TenantName}' after a successful payment.", tenantName);

            failureErrors = new Dictionary<string, string>
            {
                { "Exception", ex.Message },
            };
        }

        if (failureErrors != null)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError("Unable to auto setup a new subscribed tenant '{TenantName}'. Errors: {Errors}", tenantName, string.Join(';', failureErrors));
            }

            await TriggerTenantSetupEventAsync(workflowManager, SubscribedTenantFailedSetupEvent.EventName, tenantName, failureErrors);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("New subscribed tenant '{TenantName}' was auto setup successfully.", tenantName);
            }

            await TriggerTenantSetupEventAsync(workflowManager, SubscribedTenantSetupSucceededEvent.EventName, tenantName, errors: null);
        }
    }

    private async Task TriggerTenantSetupEventAsync(IWorkflowManager workflowManager, string eventName, string tenantName, IDictionary<string, string> errors)
    {
        if (workflowManager == null)
        {
            return;
        }

        var input = new Dictionary<string, object>
        {
            { "TenantName", tenantName },
        };

        if (errors != null)
        {
            input["Errors"] = errors;
        }

        try
        {
            await workflowManager.TriggerEventAsync(eventName, input, correlationId: $"TenantAutoSetup_{tenantName}");
        }
        catch (Exception ex)
        {
            // A failure to raise the notification must never escape post-payment completion.
            _logger.LogError(ex, "Failed to trigger the '{EventName}' workflow event for tenant '{TenantName}'.", eventName, tenantName);
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
