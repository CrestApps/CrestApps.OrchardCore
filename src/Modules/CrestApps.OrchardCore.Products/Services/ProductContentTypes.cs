using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Remembers, per tenant, which content types are products and which are product listings, for the Commerce
/// admin menu and its content lists.
/// </summary>
/// <remarks>
/// The names are read once from the content definitions and kept until a definition changes: creating, updating,
/// importing or removing a type, or attaching or detaching a part, drops the snapshot, and the next read builds a
/// new one. Registered as a singleton, so it is shared by every request of the tenant.
/// </remarks>
public sealed class ProductContentTypes : IContentDefinitionEventHandler
{
    private volatile Snapshot _snapshot;

    /// <summary>
    /// The content types that carry the product part, by name.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> GetProductTypesAsync(IContentDefinitionManager contentDefinitionManager)
        => (await GetSnapshotAsync(contentDefinitionManager)).ProductTypes;

    /// <summary>
    /// The content types that carry the product listing part, by name.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> GetListingTypesAsync(IContentDefinitionManager contentDefinitionManager)
        => (await GetSnapshotAsync(contentDefinitionManager)).ListingTypes;

    private async ValueTask<Snapshot> GetSnapshotAsync(IContentDefinitionManager contentDefinitionManager)
    {
        var snapshot = _snapshot;

        if (snapshot is not null)
        {
            return snapshot;
        }

        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        snapshot = new Snapshot(
            Select(definitions, nameof(ProductPart)),
            Select(definitions, nameof(ProductListingPart)));

        _snapshot = snapshot;

        return snapshot;
    }

    private static string[] Select(IEnumerable<ContentTypeDefinition> definitions, string partName)
        => definitions
            .Where(definition => definition.Parts.Any(part => part.PartDefinition.Name == partName))
            .Select(definition => definition.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void Reset()
        => _snapshot = null;

    public void ContentTypeCreated(ContentTypeCreatedContext context) => Reset();

    public void ContentTypeUpdated(ContentTypeUpdatedContext context) => Reset();

    public void ContentTypeImported(ContentTypeImportedContext context) => Reset();

    public void ContentTypeRemoved(ContentTypeRemovedContext context) => Reset();

    public void ContentPartAttached(ContentPartAttachedContext context) => Reset();

    public void ContentPartDetached(ContentPartDetachedContext context) => Reset();

    public void ContentTypeImporting(ContentTypeImportingContext context)
    {
    }

    public void ContentPartCreated(ContentPartCreatedContext context)
    {
    }

    public void ContentPartUpdated(ContentPartUpdatedContext context)
    {
    }

    public void ContentPartRemoved(ContentPartRemovedContext context)
    {
    }

    public void ContentPartImporting(ContentPartImportingContext context)
    {
    }

    public void ContentPartImported(ContentPartImportedContext context)
    {
    }

    public void ContentTypePartUpdated(ContentTypePartUpdatedContext context)
    {
    }

    public void ContentFieldAttached(ContentFieldAttachedContext context)
    {
    }

    public void ContentFieldUpdated(ContentFieldUpdatedContext context)
    {
    }

    public void ContentFieldDetached(ContentFieldDetachedContext context)
    {
    }

    public void ContentPartFieldUpdated(ContentPartFieldUpdatedContext context)
    {
    }

    private sealed record Snapshot(string[] ProductTypes, string[] ListingTypes);
}
