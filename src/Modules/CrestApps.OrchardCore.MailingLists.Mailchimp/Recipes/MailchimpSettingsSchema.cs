using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Recipes;

/// <summary>
/// Describes <see cref="MailchimpSettings"/> in the Settings recipe step.
/// </summary>
internal sealed class MailchimpSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name
        => nameof(MailchimpSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Mailchimp connection of mailing lists. The API key is never here: create it with the Secrets step as a text secret and name it in SecretName.")
            .Properties(
                (nameof(MailchimpSettings.SecretName), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The name of the Orchard text secret that holds the Mailchimp API key, such as Mailchimp.ApiKey. Empty turns Mailchimp off.")),
                (nameof(MailchimpSettings.ServerPrefix), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Pattern("^[a-zA-Z]{2,5}[0-9]{1,3}$")
                    .Description("The Mailchimp data center, such as us21. Empty takes it from the API key's suffix.")))
            .AdditionalProperties(true);
}
