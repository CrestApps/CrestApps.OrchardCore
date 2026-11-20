using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.Conditions;

/// <summary>
/// Reads a content field's stored value for the rules of other fields. The Conditional Fields feature has readers for
/// Orchard Core's fields and the CrestApps fields; a module adds one for its own field type. A field type without a
/// reader can't be read by a rule: conditions on it are left out.
/// </summary>
public interface IContentFieldValueReader
{
    /// <summary>
    /// Gets the field type it reads, such as <c>TextField</c>.
    /// </summary>
    string FieldType { get; }

    /// <summary>
    /// Reads the field's value.
    /// </summary>
    /// <param name="field">The field's stored JSON, or <see langword="null"/> when the item holds none.</param>
    /// <param name="definition">The field's definition.</param>
    /// <returns>The value; <see cref="ConditionValue.Empty"/> when it holds nothing.</returns>
    Task<ConditionValue> ReadAsync(JsonNode field, ContentPartFieldDefinition definition);

    /// <summary>
    /// Describes the field as a source: its options and, for a tree, the values above each value. The name, display
    /// name, kind and input name are filled in by the caller.
    /// </summary>
    /// <param name="definition">The field's definition.</param>
    /// <param name="descriptor">The descriptor to complete.</param>
    /// <returns>A task.</returns>
    Task DescribeAsync(ContentPartFieldDefinition definition, ContentConditionSourceDescriptor descriptor);
}
