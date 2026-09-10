using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "ContactCenter";
    public const string RecordingMediaDeleted = nameof(RecordingMediaDeleted);

    public void Configure(AuditTrailOptions options)
    {
        options.For<ContactCenterAuditTrailEventConfiguration>(CategoryName, S => S["Contact Center"])
            .WithEvent(
                RecordingMediaDeleted,
                S => S["Recording media deleted"],
                S => S["Recording media deletion was confirmed by the configured media store."],
                enableByDefault: true);
    }
}
