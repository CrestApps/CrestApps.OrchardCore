namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterSupportMatrix
{
    public required ContactCenterTenantProfile[] TenantProfiles { get; init; }

    public static Task<ContactCenterSupportMatrix> LoadAsync()
    {
        var matrix = new ContactCenterSupportMatrix
        {
            TenantProfiles =
            [
                new ContactCenterTenantProfile
                {
                    Id = "ga-core-asterisk",
                    ProviderProfile = "asterisk-ga-core",
                    Features =
                    [
                        "CrestApps.OrchardCore.ContactCenter",
                        "CrestApps.OrchardCore.ContactCenter.Agents",
                        "CrestApps.OrchardCore.ContactCenter.Queues",
                        "CrestApps.OrchardCore.ContactCenter.InboundVoice",
                        "CrestApps.OrchardCore.Telephony.SoftPhone",
                        "CrestApps.OrchardCore.ContactCenter.Dialer",
                        "CrestApps.OrchardCore.Asterisk",
                    ],
                },
                new ContactCenterTenantProfile
                {
                    Id = "ga-core-dialpad",
                    ProviderProfile = "dialpad-ga-core",
                    Features =
                    [
                        "CrestApps.OrchardCore.ContactCenter",
                        "CrestApps.OrchardCore.ContactCenter.Agents",
                        "CrestApps.OrchardCore.ContactCenter.Queues",
                        "CrestApps.OrchardCore.ContactCenter.InboundVoice",
                        "CrestApps.OrchardCore.Telephony.SoftPhone",
                        "CrestApps.OrchardCore.ContactCenter.Dialer",
                        "CrestApps.OrchardCore.Dialpad",
                    ],
                },
            ],
        };

        return Task.FromResult(matrix);
    }
}
