using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Chat.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="AIProfilePart"/> payload.
/// </summary>
public sealed class AIProfilePartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(AIProfilePart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ProfileId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The AI profile identifier to render for the chat widget.")),
                ("TotalHistory", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder()
                        .Type(SchemaValueType.Integer)
                        .Description("The number of chat history entries to show. Omit or set null to disable history rendering."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
}
