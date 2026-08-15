using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Taxonomies;
using OrchardCore.Taxonomies.Fields;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Inherits a content item's tax classification from the taxonomy terms (product categories) it belongs to.
/// Each taxonomy term can carry its own <see cref="TaxationPart"/>, so tax codes are managed per category
/// and every item categorized under a term inherits that term's classification unless it classifies itself.
/// </summary>
/// <remarks>
/// The provider inspects every <see cref="TaxonomyField"/> attached to the item's content type, walks the
/// selected terms in order, and returns the first term that carries a taxable classification with a category
/// code. This keeps taxation decoupled from taxonomies: it only participates when the
/// <c>OrchardCore.Taxonomies</c> feature is enabled.
/// </remarks>
internal sealed class TaxonomyTaxClassificationProvider : ITaxClassificationProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IOrchardHelper _orchardHelper;

    public TaxonomyTaxClassificationProvider(
        IContentDefinitionManager contentDefinitionManager,
        IOrchardHelper orchardHelper)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _orchardHelper = orchardHelper;
    }

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async ValueTask<TaxClassification> GetClassificationAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (definition?.Parts is null)
        {
            return null;
        }

        foreach (var partDefinition in definition.Parts)
        {
            var fields = partDefinition.PartDefinition?.Fields;

            if (fields is null)
            {
                continue;
            }

            foreach (var fieldDefinition in fields)
            {
                if (!string.Equals(fieldDefinition.FieldDefinition?.Name, nameof(TaxonomyField), StringComparison.Ordinal))
                {
                    continue;
                }

                JsonObject content = contentItem.Content;
                var field = content?[partDefinition.Name]?[fieldDefinition.Name]?.ToObject<TaxonomyField>();

                if (field is null ||
                    string.IsNullOrEmpty(field.TaxonomyContentItemId) ||
                    field.TermContentItemIds is null ||
                    field.TermContentItemIds.Length == 0)
                {
                    continue;
                }

                foreach (var termId in field.TermContentItemIds)
                {
                    if (string.IsNullOrEmpty(termId))
                    {
                        continue;
                    }

                    var term = await _orchardHelper.GetTaxonomyTermAsync(field.TaxonomyContentItemId, termId);

                    var part = term?.Get<TaxationPart>(nameof(TaxationPart));

                    if (part is not null && part.Taxable && !string.IsNullOrEmpty(part.TaxCategoryCode))
                    {
                        return new TaxClassification
                        {
                            TaxCategoryCode = part.TaxCategoryCode,
                            TaxClassificationCode = part.TaxClassificationCode,
                            ExternalTaxCode = part.ExternalTaxCode,
                        };
                    }
                }
            }
        }

        return null;
    }
}
