using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// One report field of a content type data set: its descriptor and how its value is read from a content item. Field
/// and part providers create these; the content data source converts every value they return to the CLR type of
/// <see cref="ReportFieldDescriptor.DataType"/>, so an implementation may return raw JSON nodes.
/// </summary>
public abstract class ContentReportField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReportField"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor of the field.</param>
    protected ContentReportField(ReportFieldDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    /// <summary>
    /// Gets the descriptor of the field. Its name is stored by report designs, so it must stay stable.
    /// </summary>
    public ReportFieldDescriptor Descriptor { get; }

    /// <summary>
    /// Loads anything the field needs for all the content items of one query, such as the display texts of
    /// referenced content items. The data source calls this once per query, and only when the report uses the
    /// field, before it calls <see cref="GetValue(ContentItem, ContentReportQueryContext)"/>.
    /// </summary>
    /// <param name="context">The query context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the field is ready.</returns>
    public virtual Task PrepareAsync(ContentReportQueryContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the value of the field from a content item.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="context">The query context.</param>
    /// <returns>The raw value, or <see langword="null"/> when the content item has no value.</returns>
    public abstract object GetValue(ContentItem contentItem, ContentReportQueryContext context);
}
