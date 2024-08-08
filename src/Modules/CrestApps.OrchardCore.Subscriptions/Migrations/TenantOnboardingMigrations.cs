using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class TenantOnboardingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TenantOnboardingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("TenantOnboardingPart", part => part
            .Attachable()
            .WithDisplayName("Tenant Onboarding")
            .WithDescription("Provides the key properties for tenant onboarding subscription.")
        );

        return 1;
    }
}
