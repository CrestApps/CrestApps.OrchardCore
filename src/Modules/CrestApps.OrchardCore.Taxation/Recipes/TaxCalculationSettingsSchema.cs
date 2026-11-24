using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Taxation.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>TaxCalculationSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxCalculationSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(TaxCalculationSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How the site works tax out. The checkout and invoices both read it.")
            .Properties(
                ("RoundingLevel", new JsonSchemaBuilder()
                    .AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(Enum.GetNames<TaxRoundingLevel>()), new JsonSchemaBuilder().Type(SchemaValueType.Integer))
                    .Description("The level tax is rounded at: Rate rounds each tax and rate once on the summed amount (EN 16931, the default of a new site), Line rounds each line's tax, Tax each kind of tax, Jurisdiction each jurisdiction, Transaction only the total.")))
            .AdditionalProperties(true);
}
