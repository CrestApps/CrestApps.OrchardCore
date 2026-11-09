using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PayLater.Recipes;

/// <summary>
/// Describes the <c>PayLaterSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayLaterSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(PayLaterSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings that control how Pay Later commitments become outstanding transactions.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether Pay Later is offered at checkout. Off by default, so a store doesn't let every shopper order without paying until it chooses to.")),
                ("NetTermDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(30).Description("The number of days a Pay Later balance is allowed before it is due. When greater than zero the outstanding transaction is created with a due date this many days after checkout, which drives the reminder cadence. A value of 0 leaves the balance without a due date. Defaults to 30.")))
            .AdditionalProperties(true);
}
