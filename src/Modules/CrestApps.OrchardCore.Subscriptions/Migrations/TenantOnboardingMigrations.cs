using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Adds the content definition metadata for tenant onboarding subscriptions.
/// </summary>
public sealed class TenantOnboardingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOnboardingMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to update content part definitions.</param>
    public TenantOnboardingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the tenant onboarding content part definition.
    /// </summary>
    /// <returns>The migration version after the tenant onboarding definition is created.</returns>
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
