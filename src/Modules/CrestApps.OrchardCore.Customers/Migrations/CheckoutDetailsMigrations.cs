using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Customers.Migrations;

/// <summary>
/// Creates the Checkout details content type, with no fields: the checkout asks for nothing until the store adds some.
/// </summary>
internal sealed class CheckoutDetailsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CheckoutDetailsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // The type's own part holds the fields, as the content type editor adds them.
        await _contentDefinitionManager.AlterPartDefinitionAsync(CustomersConstants.CheckoutDetailsContentType, part => part
            .WithDescription("The fields the checkout asks buyers for.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(CustomersConstants.CheckoutDetailsContentType, type => type
            .WithDisplayName("Checkout details")
            .WithDescription("The store's own checkout fields. Each field added here is asked for on the checkout's Contact step, and what the buyer enters is kept on the order.")
            .Creatable(false)
            .Listable(false)
            .Draftable(false)
            .Versionable(false)
            .Securable(false)
            .WithPart(CustomersConstants.CheckoutDetailsContentType, part => part
                .WithPosition("0")
            )
        );

        return 1;
    }
}
