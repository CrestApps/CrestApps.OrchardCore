using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Shipping.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// Describes the <c>EasyPostSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class EasyPostSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(EasyPostSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The store's EasyPost account, on the EasyPost tab of Settings › Commerce › Shipping. The API key and the webhook secret are kept encrypted.")
            .Properties(
                ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the store uses EasyPost as a carrier provider.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The EasyPost API key. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("WebhookSecret", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The secret EasyPost signs its tracking updates with, made when the webhook is registered. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("WebhookId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("EasyPost's id for the webhook registered for this site, or null.")),
                ("WebhookUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The address the webhook was registered for.")))
            .AdditionalProperties(true);
}
