using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PayLater.Recipes;

/// <summary>
/// Describes the <c>BankTransferSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class BankTransferSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(BankTransferSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings of the Bank transfer payment method: whether it is offered, how to pay, and when it is due.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether Bank transfer is offered at checkout. Off until the store adds its bank details.")),
                ("Instructions", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("How to pay, shown at checkout, on the confirmation and on the order's page: the store's bank account details, for example.")),
                ("NetTermDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(7).Description("How many days after checkout the transfer is due. 0 records the balance without a due date.")))
            .AdditionalProperties(true);
}
