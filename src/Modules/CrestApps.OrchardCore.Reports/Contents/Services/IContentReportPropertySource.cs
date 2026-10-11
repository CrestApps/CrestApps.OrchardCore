using CrestApps.OrchardCore.Reports.Contents.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Describes the properties a content part stores, so each one can be a report column even when no
/// <see cref="IContentReportPartProvider"/> knows the part. The content items data source asks every source for the
/// parts of a content type, and also discovers the properties of the parts from the content items it stores; a typed
/// property from a source wins over one inferred from stored values.
/// </summary>
public interface IContentReportPropertySource
{
    /// <summary>
    /// Gets the properties of a part of a content type.
    /// </summary>
    /// <param name="typePart">The part, as the content type uses it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The properties, by their path within the part; empty when the source does not know the part.</returns>
    ValueTask<IReadOnlyList<ContentReportProperty>> GetPropertiesAsync(ContentTypePartDefinition typePart, CancellationToken cancellationToken = default);
}
