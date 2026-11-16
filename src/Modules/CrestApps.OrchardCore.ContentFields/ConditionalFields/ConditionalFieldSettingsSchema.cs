using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Adds <c>ConditionalFieldSettings</c> to the settings schema of a field type in the content definition recipe steps.
/// Its fragment is combined with the field type's own settings schema.
/// </summary>
internal sealed class ConditionalFieldSettingsSchema : IContentSchemaDefinition
{
    /// <summary>
    /// The field types the settings are described for: Orchard Core's and the CrestApps fields.
    /// </summary>
    public static readonly string[] FieldTypes =
    [
        "TextField", "BooleanField", "NumericField", "DateField", "DateTimeField", "TimeField", "MultiTextField",
        "ContentPickerField", "UserPickerField", "LocalizationSetContentPickerField", "LinkField", "HtmlField",
        "MarkdownField", "TaxonomyField", "MediaField", "YoutubeField", "GeoPointField", "PhoneField", "AddressField",
    ];

    private static readonly JsonSchemaBuilder _schema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Properties(
            ("ConditionalFieldSettings", new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("ShowWhen", ConditionRuleSchema.Build(
                        "When the field shows (the Conditional Fields feature); null for always. What a hidden field holds is dropped when the editor is sent.",
                        "Another field of the item as {TypePartName}.{FieldName} (such as Ticket.Outage), or the name of a value a module provides (such as TicketCategory).")))
                .AdditionalProperties(true)))
        .AdditionalProperties(true);

    public ConditionalFieldSettingsSchema(string fieldType)
    {
        Name = fieldType;
    }

    public ContentDefinitionSchemaType Type => ContentDefinitionSchemaType.Field;

    public string Name { get; }

    public ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_schema);
}
