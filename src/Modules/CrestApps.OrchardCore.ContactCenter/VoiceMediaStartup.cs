using CrestApps.Core.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using CrestApps.OrchardCore.ContactCenter.Core;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers provider-capability-gated bidirectional voice-media resolution.
/// </summary>
[Feature(ContactCenterFeatures.VoiceMedia)]
public sealed class VoiceMediaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterCapability(ContactCenterFeatures.VoiceMedia, ContactCenterCapabilities.VoiceMedia);

        services.AddCoreContactCenterVoiceMedia();
    }
}
