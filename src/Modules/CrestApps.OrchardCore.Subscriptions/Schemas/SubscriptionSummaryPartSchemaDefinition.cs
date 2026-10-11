using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="SubscriptionSummaryPart"/> marker part.
/// The part carries no persisted data; its display driver computes live statistics at render time.
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
            .Description("A marker part that carries no persisted data.")
            .AdditionalProperties(true);
}
