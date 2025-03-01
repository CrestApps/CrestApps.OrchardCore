using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface INamedModelManager<T> : IModelManager<T>
    where T : INameAwareModel, new()
{
    ValueTask<T> FindByNameAsync(string name);
}
