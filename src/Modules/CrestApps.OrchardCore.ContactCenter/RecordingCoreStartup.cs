using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the shared recording-access governance services. These are the audit-and-gate services that both the
/// full call-recording feature and voicemail playback rely on, so they live in their own dependency-enabled core
/// feature: a deployment can play and audit voicemail (a Voice capability) without enabling full call recording,
/// while the recording feature reuses the exact same governance rather than duplicating it.
/// </summary>
[Feature(ContactCenterConstants.Feature.RecordingCore)]
public sealed class RecordingCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecordingAccessGovernanceService, RecordingAccessGovernanceService>();
    }
}
