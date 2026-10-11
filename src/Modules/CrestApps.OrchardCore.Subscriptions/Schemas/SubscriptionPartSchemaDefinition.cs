using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="SubscriptionPart"/> payload.
/// </summary>
public sealed class SubscriptionPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(SubscriptionPart);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("SubscriptionPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                            .Description("The content types for which the subscription flow collects content item data.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("InitialAmountDescription", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The line item description for the initial one-time amount.")),
                ("InitialAmount", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number | SchemaValueType.Null)
                    .Description("The one-time amount charged when the subscription starts, in major currency units.")),
                ("BillingDuration", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Description("The number of duration units in one billing cycle.")),
                ("DurationType", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Year", "Month", "Week", "Day")
                    .Description("The unit used with the billing duration to define the billing cycle length.")),
                ("BillingCycleLimit", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The maximum number of billing cycles to process before the subscription ends.")),
                ("SubscriptionDayDelay", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The number of days to delay the first recurring subscription payment.")),
                ("Sort", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The sort position for displaying the subscription.")))
            .AdditionalProperties(true);
}
