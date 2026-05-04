using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Document-backed implementation of <see cref="INamedCatalog{T}"/> that extends <see cref="Catalog{T}"/>
/// with name-based lookup and uniqueness enforcement.
/// </summary>
/// <typeparam name="T">The type of named catalog item managed by this catalog.</typeparam>
public class NamedCatalog<T> : Catalog<T>, INamedCatalog<T>
    where T : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NamedCatalog{T}"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for accessing the backing document.</param>
    public NamedCatalog(IDocumentManager<DictionaryDocument<T>> documentManager)
        : base(documentManager)
    {
    }

    /// <inheritdoc />
    public async ValueTask<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var record = document.Records.Values.FirstOrDefault(x => OrdinalIgnoreCaseEquals(x.Name, name));

        return Clone(record);
    }

    protected override void Saving(T record, DictionaryDocument<T> document)
    {
        if (document.Records.Values.Any(x => OrdinalIgnoreCaseEquals(x.Name, record.Name) && x.ItemId != record.ItemId))
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }

    protected static bool OrdinalIgnoreCaseEquals(string str1, string str2)
    {
        return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
    }
}
