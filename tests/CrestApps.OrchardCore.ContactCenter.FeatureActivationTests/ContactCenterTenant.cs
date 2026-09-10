using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterTenant
{
    public ContactCenterTenant(
        ShellSettings settings,
        ContactCenterTenantProfile profile)
    {
        Settings = settings;
        Profile = profile;
    }

    public ShellSettings Settings { get; }

    public ContactCenterTenantProfile Profile { get; }
}
