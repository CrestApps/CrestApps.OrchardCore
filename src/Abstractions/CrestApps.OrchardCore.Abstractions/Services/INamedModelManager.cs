using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Services;

public interface INamedModelManager<T> : IModelManager<T>
    where T : INameAwareModel
{
    ValueTask<T> FindByNameAsync(string name);
}
