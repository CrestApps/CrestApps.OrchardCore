using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Plaid.Recipes;

/// <summary>
/// Describes <see cref="PlaidSettings"/> in the Settings recipe step.
/// </summary>
internal sealed class PlaidSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name
        => nameof(PlaidSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Plaid settings. The Plaid secret is never here: create it with the Secrets step and name it in SecretName.")
            .Properties(
                (nameof(PlaidSettings.Environment), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Integer)
                    .Description("Sandbox (0) for Plaid's test banks, Production (1) for real banks.")),
                (nameof(PlaidSettings.ClientId), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The client id from the Plaid dashboard. Empty turns Plaid off.")),
                (nameof(PlaidSettings.SecretName), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The name of the Orchard text secret that holds the Plaid secret of the environment, such as Plaid.Secret.")))
            .AdditionalProperties(true);
}
