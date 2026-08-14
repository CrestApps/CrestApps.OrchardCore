using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dialpad;

/// <summary>
/// Registers the Dialpad implementation of the Contact Center voice provider boundary.
/// </summary>
[Feature(DialpadConstants.Feature.ContactCenterVoice)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<DialpadContactCenterVoiceProvider>()
            .AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<DialpadContactCenterVoiceProvider>())
            .AddSingleton<IProviderIdentityProvider, DialpadProviderIdentityProvider>()
            .AddScoped<IDialpadWebhookService, DialpadWebhookService>()
            .AddScoped<IProviderWebhookInboxHandler, DialpadWebhookInboxHandler>()
            .AddScoped<DialpadContactCenterFeatureLifecycleParticipant>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                serviceProvider.GetRequiredService<DialpadContactCenterFeatureLifecycleParticipant>());
    }
}
