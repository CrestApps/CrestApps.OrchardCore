using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Keeps one default template per document kind: a template saved as the default takes over from the others that claim
/// the same kinds, or that claim every kind when it does too. The Document templates page and the generic record tools
/// both apply it after a save.
/// </summary>
internal static class BusinessDocumentTemplateDefaults
{
    /// <summary>
    /// Takes the default from the other templates that claim a saved default template's kinds.
    /// </summary>
    /// <param name="manager">The template manager.</param>
    /// <param name="template">The template just saved.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The templates that are no longer the default.</returns>
    public static async Task<IReadOnlyList<BusinessDocumentTemplate>> TakeOverDefaultAsync(ICatalogManager<BusinessDocumentTemplate> manager, BusinessDocumentTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(template);

        if (!template.IsDefault)
        {
            return [];
        }

        var replaced = new List<BusinessDocumentTemplate>();

        foreach (var other in await manager.GetAllAsync(cancellationToken))
        {
            if (other.ItemId != template.ItemId && other.IsDefault && ClaimTheSameKinds(template, other))
            {
                other.IsDefault = false;
                await manager.UpdateAsync(other, cancellationToken: cancellationToken);
                replaced.Add(other);
            }
        }

        return replaced;
    }

    private static bool ClaimTheSameKinds(BusinessDocumentTemplate first, BusinessDocumentTemplate second)
    {
        var firstKinds = first.Kinds ?? [];
        var secondKinds = second.Kinds ?? [];

        if (firstKinds.Count == 0 || secondKinds.Count == 0)
        {
            return firstKinds.Count == 0 && secondKinds.Count == 0;
        }

        return firstKinds.Intersect(secondKinds, StringComparer.OrdinalIgnoreCase).Any();
    }
}
