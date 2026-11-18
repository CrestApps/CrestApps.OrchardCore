using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Adds the <c>BusinessDocument__{Kind}</c> alternate to the <c>BusinessDocument</c> shape, so a theme or a module can
/// give one kind of document its own look, in a <c>BusinessDocument-{Kind}.cshtml</c> view.
/// </summary>
internal sealed class BusinessDocumentShapeTableProvider : IShapeTableProvider
{
    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(BusinessDocumentsConstants.ShapeType)
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape.Properties.TryGetValue("Document", out var value) &&
                    value is BusinessDocument document &&
                    BusinessDocumentStyle.GetAlternateName(document.Kind) is { } kind)
                {
                    displaying.Shape.Metadata.Alternates.Add($"{BusinessDocumentsConstants.ShapeType}__{kind}");
                }
            });

        return ValueTask.CompletedTask;
    }
}
