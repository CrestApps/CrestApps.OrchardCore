using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Describes the <c>AICostSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class AICostSettingsSchema : SiteSettingsSchemaBase
{
    /// <inheritdoc />
    public override string Name => nameof(AICostSettings);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What the phone lines cost, used to estimate the cost of each AI call. What the AI costs is set on each AI deployment (its Pricing).")
            .Properties(
                (nameof(AICostSettings.Currency), new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[A-Za-z]{3}$").Default("USD").Description("The ISO currency code the carrier's prices are in, such as USD.")),
                (nameof(AICostSettings.TelephonyPerMinute), new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Description("What a minute of a phone call costs with the carrier.")),
                (nameof(AICostSettings.SmsPerMessage), new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Description("What one text message costs with the carrier.")))
            .AdditionalProperties(true);
}
