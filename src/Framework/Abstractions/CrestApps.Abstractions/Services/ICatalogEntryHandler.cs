using CrestApps.Models;

namespace CrestApps.Services;

public interface ICatalogEntryHandler<T>
{
    Task InitializingAsync(InitializingContext<T> context);
    Task InitializedAsync(InitializedContext<T> context);
    Task LoadedAsync(LoadedContext<T> context);
    Task ValidatingAsync(ValidatingContext<T> context);
    Task ValidatedAsync(ValidatedContext<T> context);
    Task DeletingAsync(DeletingContext<T> context);
    Task DeletedAsync(DeletedContext<T> context);
    Task UpdatingAsync(UpdatingContext<T> context);
    Task UpdatedAsync(UpdatedContext<T> context);
    Task CreatingAsync(CreatingContext<T> context);
    Task CreatedAsync(CreatedContext<T> context);
}
