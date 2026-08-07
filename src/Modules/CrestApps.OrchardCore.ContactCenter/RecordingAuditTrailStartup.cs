using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Orchard Audit Trail receipt for confirmed recording-media deletion.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class RecordingAuditTrailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ContactCenterAuditTrailEventConfiguration>();
        services.AddScoped<IContactCenterEventHandler, RecordingMediaDeletionAuditTrailHandler>();
    }
}
