using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceCatalog<T> : SourceCatalog<T>, INamedSourceCatalog<T>, ISourceCatalog<T>
    where T : CatalogEntry, INameAwareModel, ISourceAwareModel
{
    public NamedSourceCatalog(IDocumentManager<DictionaryDocument<T>> documentManager)
        : base(documentManager)
    {
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var record = document.Records.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Clone(record);
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var record = document.Records.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Source.Equals(source, StringComparison.OrdinalIgnoreCase));

        return Clone(record);
    }

    protected override void Saving(T record, DictionaryDocument<T> document)
    {
        if (document.Records.Values.Any(x => x.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) && x.Id != record.Id))
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
