using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// Describes the content part, or the content field of a part, a provider describes report fields for. Report
/// field names follow the pattern <c>{PartName}.{FieldName}</c> for a content field and <c>{PartName}</c> for a
/// part, with an optional <c>.{Suffix}</c> for each extra value (for example <c>Order.Customer.DisplayText</c>).
/// </summary>
public sealed class ContentReportFieldContext
{
    /// <summary>
    /// Gets the content type the data set reads.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; init; }

    /// <summary>
    /// Gets the part of the content type.
    /// </summary>
    public ContentTypePartDefinition TypePartDefinition { get; init; }

    /// <summary>
    /// Gets the content field of the part, or <see langword="null"/> when a part provider describes the part itself.
    /// </summary>
    public ContentPartFieldDefinition PartFieldDefinition { get; init; }

    /// <summary>
    /// Gets the name of the part in the content type, which is also its JSON property name in the content item.
    /// </summary>
    public string PartName => TypePartDefinition?.Name;

    /// <summary>
    /// Gets the name of the content field in the part, or <see langword="null"/> for a part.
    /// </summary>
    public string FieldName => PartFieldDefinition?.Name;

    /// <summary>
    /// Gets the base report field name: <c>{PartName}.{FieldName}</c> for a content field, <c>{PartName}</c> for a part.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the label of the content field, or of the part when a part provider describes the part itself.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Gets the designer group of the report fields, which is the display name of the part.
    /// </summary>
    public string Group { get; init; }

    /// <summary>
    /// Builds the stable report field name for an optional suffix.
    /// </summary>
    /// <param name="suffix">The optional suffix, such as <c>DisplayText</c>.</param>
    /// <returns><see cref="Name"/>, followed by <c>.{suffix}</c> when a suffix is given.</returns>
    public string GetFieldName(string suffix = null)
    {
        return string.IsNullOrEmpty(suffix)
            ? Name
            : $"{Name}.{suffix}";
    }

    /// <summary>
    /// Creates the descriptor of a report field in the group of the part.
    /// </summary>
    /// <param name="suffix">The optional suffix of the field name. See <see cref="GetFieldName(string)"/>.</param>
    /// <param name="displayName">The label shown in the designer.</param>
    /// <param name="dataType">The data type of the values.</param>
    /// <param name="isIdentifier">Whether the field identifies a record, so the designer suggests it for joins.</param>
    /// <returns>The descriptor.</returns>
    public ReportFieldDescriptor CreateDescriptor(string suffix, string displayName, ReportDataType dataType, bool isIdentifier = false)
    {
        return new ReportFieldDescriptor(GetFieldName(suffix), displayName, dataType, Group)
        {
            IsIdentifier = isIdentifier,
        };
    }

    /// <summary>
    /// Creates a report field that reads a JSON property of the content field, or of the part when
    /// <see cref="PartFieldDefinition"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="suffix">The optional suffix of the field name. See <see cref="GetFieldName(string)"/>.</param>
    /// <param name="displayName">The label shown in the designer.</param>
    /// <param name="dataType">The data type of the values.</param>
    /// <param name="mode">How the property becomes a value.</param>
    /// <param name="propertyNames">The property names to try, in order.</param>
    /// <returns>The report field.</returns>
    public ContentElementReportField CreateElementField(
        string suffix,
        string displayName,
        ReportDataType dataType,
        ContentReportValueMode mode,
        params string[] propertyNames)
    {
        return new ContentElementReportField(
            CreateDescriptor(suffix, displayName, dataType),
            PartName,
            FieldName,
            mode,
            propertyNames);
    }
}
