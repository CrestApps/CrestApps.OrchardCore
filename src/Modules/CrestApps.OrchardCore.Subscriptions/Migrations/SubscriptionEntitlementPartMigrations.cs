using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Defines the part that records what a subscription plan entitles a subscriber to.
/// </summary>
public sealed class SubscriptionEntitlementPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionEntitlementPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SubscriptionEntitlementPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the entitlement part definition.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        // The part is attachable rather than injected, because not every subscription plan grants member
        // access. A plan that sells a service or a tenant rather than site membership should not carry an
        // empty role picker.
        await _contentDefinitionManager.AlterPartDefinitionAsync("SubscriptionEntitlementPart", part => part
            .Attachable()
            .WithDisplayName("Subscription Entitlements")
            .WithDescription("Grants roles to a subscriber for as long as their subscription is current.")
        );

        return 1;
    }
}
