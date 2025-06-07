using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class NamedModelStore<T> : ModelStore<T>, INamedModelStore<T>
    where T : Model, INameAwareModel
{
    public NamedModelStore(IDocumentManager<ModelDocument<T>> documentManager)
        : base(documentManager)
    {
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    protected override void Saving(T record, ModelDocument<T> document)
    {
        if (document.Records.Values.Any(x => x.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) && x.Id != record.Id))
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
