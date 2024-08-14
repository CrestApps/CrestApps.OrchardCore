using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class TenantOnboardingSubscriptionHandler : SubscriptionHandlerBase
{
    private readonly IShellHost _shellHost;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public TenantOnboardingSubscriptionHandler(
        IShellHost shellHost,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _shellHost = shellHost;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
        {
            return Task.CompletedTask;
        }

        context.Session.Steps.Add(new SubscriptionFlowStep()
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
        });

        return Task.CompletedTask;
    }

    public override Task CompletingAsync(SubscriptionFlowCompletedContext context)
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

    public override Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        if (!context.Flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
        {
            return Task.CompletedTask;
        }

        var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

        var domains = info.GetDomains();

        // Create the new tenant.

        return Task.CompletedTask;
    }
}
