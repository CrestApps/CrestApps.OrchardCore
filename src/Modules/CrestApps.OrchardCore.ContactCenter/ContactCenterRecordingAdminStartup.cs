using CrestApps.OrchardCore.ContactCenter.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the recording and monitoring settings screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Admin)]
[RequireFeatures(ContactCenterConstants.Feature.Recording)]
public sealed class ContactCenterRecordingAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<ContactCenterRecordingSettingsDisplayDriver>();
    }
}
