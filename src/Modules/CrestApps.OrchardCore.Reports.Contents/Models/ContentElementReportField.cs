using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// A report field read from a JSON property of a content part (<c>Content[partName][property]</c>) or of a content
/// field (<c>Content[partName][fieldName][property]</c>).
/// </summary>
public sealed class ContentElementReportField : ContentReportField
{
    private readonly string _partName;
    private readonly string _fieldName;
    private readonly ContentReportValueMode _mode;
    private readonly string[] _propertyNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentElementReportField"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor of the report field.</param>
    /// <param name="partName">The name of the part in the content type.</param>
    /// <param name="fieldName">The name of the content field in the part, or <see langword="null"/> to read a property of the part itself.</param>
    /// <param name="mode">How the property becomes a value.</param>
    /// <param name="propertyNames">The property names to try, in order. The first one present with a value wins.</param>
    public ContentElementReportField(
        ReportFieldDescriptor descriptor,
        string partName,
        string fieldName,
        ContentReportValueMode mode,
        params string[] propertyNames)
        : base(descriptor)
    {
        _partName = partName;
        _fieldName = fieldName;
        _mode = mode;
        _propertyNames = propertyNames;
    }

    /// <inheritdoc/>
    public override object GetValue(ContentItem contentItem, ContentReportQueryContext context)
    {
        var element = ContentReportJson.GetElement(contentItem, _partName, _fieldName);

        return ContentReportJson.ReadProperty(element, _mode, _propertyNames);
    }
}
