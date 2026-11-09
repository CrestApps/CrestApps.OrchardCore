using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Describes the <c>ContactCenterDefaultAddressSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ContactCenterDefaultAddressSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ContactCenterDefaultAddressSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The numbers used when an agent has none of their own: the number calls are placed from and the number texts are sent from.")
            .Properties(
                (nameof(ContactCenterDefaultAddressSettings.DefaultPhoneAddressId), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The ItemId of the phone channel endpoint (see the OmnichannelChannelEndpoint step) calls are placed from by default.")),
                (nameof(ContactCenterDefaultAddressSettings.DefaultSmsAddressId), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The ItemId of the SMS channel endpoint (see the OmnichannelChannelEndpoint step) texts are sent from by default.")))
            .AdditionalProperties(true);
}
