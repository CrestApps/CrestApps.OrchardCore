using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.DialPad.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DialPad;

/// <summary>
/// Registers the DialPad implementation of the Contact Center voice provider boundary.
/// </summary>
[Feature(DialPadConstants.Feature.ContactCenterVoice)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<DialPadContactCenterVoiceProvider>()
            .AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<DialPadContactCenterVoiceProvider>())
            .AddSingleton<IProviderIdentityProvider, DialPadProviderIdentityProvider>()
            .AddScoped<IDialPadWebhookService, DialPadWebhookService>()
            .AddScoped<IProviderWebhookInboxHandler, DialPadWebhookInboxHandler>();
    }
}
