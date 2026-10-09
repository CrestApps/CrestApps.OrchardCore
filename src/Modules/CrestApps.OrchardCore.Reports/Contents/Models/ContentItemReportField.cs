using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// A report field whose value is computed from the content item by a delegate, such as a property of
/// <see cref="ContentItem"/> itself.
/// </summary>
public sealed class ContentItemReportField : ContentReportField
{
    private readonly Func<ContentItem, object> _valueAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemReportField"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor of the report field.</param>
    /// <param name="valueAccessor">Reads the value from a content item.</param>
    public ContentItemReportField(
        ReportFieldDescriptor descriptor,
        Func<ContentItem, object> valueAccessor)
        : base(descriptor)
    {
        _valueAccessor = valueAccessor;
    }

    /// <inheritdoc/>
    public override object GetValue(ContentItem contentItem, ContentReportQueryContext context)
    {
        if (contentItem is null || _valueAccessor is null)
        {
            return null;
        }

        return _valueAccessor(contentItem);
    }
}
