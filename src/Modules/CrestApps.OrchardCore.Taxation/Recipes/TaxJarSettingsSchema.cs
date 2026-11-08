using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Taxation.TaxJar;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>TaxJarSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxJarSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(TaxJarSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The TaxJar account sales tax is worked out with, under Commerce › Taxation › TaxJar.")
            .Properties(
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The TaxJar API token. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("UseSandbox", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether TaxJar's sandbox answers, for a sandbox token.")),
                ("Countries", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The two-letter codes of the countries TaxJar works tax out for; orders to the others are taxed by the store's own rules.")))
            .AdditionalProperties(true);
}
