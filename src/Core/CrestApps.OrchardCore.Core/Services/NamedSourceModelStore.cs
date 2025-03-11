using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedSourceModelStore<T> : SourceModelStore<T>, INamedSourceModelStore<T>, ISourceModelStore<T>
    where T : Model, INameAwareModel, ISourceAwareModel
{
    public NamedSourceModelStore(IDocumentManager<ModelDocument<T>> documentManager)
        : base(documentManager)
    {
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var model = document.Records.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (model is not null)
        {
            return model;
        }

        return null;
    }

    protected override void Saving(T record, ModelDocument<T> document)
    {
        if (document.Records.Values.Any(x => x.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) && x.Id != record.Id))
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var model = document.Records.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Source.Equals(source, StringComparison.OrdinalIgnoreCase));

        if (model is not null)
        {
            return model;
        }

        return null;
    }
}
