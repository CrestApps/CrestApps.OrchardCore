namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterTenantProfile
{
    public required string Id { get; init; }

    public required string ProviderProfile { get; init; }

    public required string[] Features { get; init; }
}
