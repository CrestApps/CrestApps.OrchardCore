using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// The look business documents of some kinds are printed with: the logo, the accent color, the paper size and the
/// footer. <see cref="Services.IBusinessDocumentTemplateManager.ApplyAsync"/> copies them onto a document that has
/// none of its own.
/// </summary>
public sealed class BusinessDocumentTemplate : CatalogItem, INameAwareModel, IAuditedModel
{
    /// <summary>
    /// Gets or sets the name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets notes about the template.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kinds of documents the template is for, by <see cref="BusinessDocumentKind.Name"/>. An empty
    /// list is for every kind.
    /// </summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the template is used for its kinds when a document names no template.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the address of the logo printed on the documents.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the accent color of the documents, written <c>#RRGGBB</c>.
    /// </summary>
    public string AccentColor { get; set; }

    /// <summary>
    /// Gets or sets the paper size the documents are printed on.
    /// </summary>
    public BusinessDocumentPaperSize PaperSize { get; set; }

    /// <summary>
    /// Gets or sets the text printed at the bottom of the documents.
    /// </summary>
    public string Footer { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc/>
    public string OwnerId { get; set; }

    /// <inheritdoc/>
    public string Author { get; set; }

    /// <summary>
    /// Returns whether the template is for a kind of document: it lists the kind, or lists none.
    /// </summary>
    /// <param name="kind">The kind of document.</param>
    /// <returns><see langword="true"/> when the template is for the kind.</returns>
    public bool AppliesTo(string kind)
    {
        if (Kinds is null || Kinds.Count == 0)
        {
            return true;
        }

        return !string.IsNullOrEmpty(kind) && Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase);
    }
}
