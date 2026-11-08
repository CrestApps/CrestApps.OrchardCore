using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Describes the <c>AIContactMemorySettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class AIContactMemorySettingsSchema : SiteSettingsSchemaBase
{
    /// <inheritdoc />
    public override string Name => nameof(AIContactMemorySettings);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How the AI rewrites a CRM contact's memory note after each conversation.")
            .Properties(
                (nameof(AIContactMemorySettings.Instructions), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The instructions the memory writer follows. Empty uses the built-in instructions.")))
            .AdditionalProperties(true);
}
