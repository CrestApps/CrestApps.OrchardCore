using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dialpad;

/// <summary>
/// Registers the Dialpad implementation of the Contact Center voice provider boundary. This is integration
/// glue rather than a separately selectable feature: it activates automatically whenever the Dialpad provider
/// and Contact Center Voice are both enabled, so an operator never has to enable a redundant per-provider
/// toggle that must match the provider they already configured.
/// </summary>
[RequireFeatures(DialpadConstants.Feature.Area, ContactCenterConstants.Feature.Voice)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<DialpadContactCenterVoiceProvider>()
            .AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<DialpadContactCenterVoiceProvider>())
            .AddSingleton<IProviderIdentityProvider, DialpadProviderIdentityProvider>()
            .AddScoped<IDialpadInboundCallRouter, ContactCenterDialpadInboundCallRouter>()
            .AddScoped<IProviderWebhookInboxHandler, DialpadWebhookInboxHandler>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new DialpadContactCenterFeatureLifecycleParticipant(
                    DialpadConstants.Feature.Area,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new DialpadContactCenterFeatureLifecycleParticipant(
                    ContactCenterConstants.Feature.Voice,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));
    }
}
