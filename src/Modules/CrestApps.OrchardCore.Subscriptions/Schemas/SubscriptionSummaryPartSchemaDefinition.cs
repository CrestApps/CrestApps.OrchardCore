using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="SubscriptionSummaryPart"/> part.
/// The part persists only how long the widget keeps its figures; its display driver computes the statistics at render time.
/// </summary>
public sealed class SubscriptionSummaryPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(SubscriptionSummaryPart);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("CacheMinutes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Description("How many minutes the widget keeps its figures before it counts the subscriptions again; 0 counts them on every visit. Defaults to 30.")))
            .AdditionalProperties(true);
}
