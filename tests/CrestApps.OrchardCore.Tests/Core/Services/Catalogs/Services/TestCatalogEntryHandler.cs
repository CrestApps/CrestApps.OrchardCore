using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

internal sealed class TestCatalogEntryHandler<T> : ICatalogEntryHandler<T>
{
    public Func<DeletingContext<T>, Task> OnDeletingAsync { get; set; } = _ => Task.CompletedTask;

    public Func<DeletedContext<T>, Task> OnDeletedAsync { get; set; } = _ => Task.CompletedTask;

    public Func<CreatingContext<T>, Task> OnCreatingAsync { get; set; } = _ => Task.CompletedTask;

    public Func<CreatedContext<T>, Task> OnCreatedAsync { get; set; } = _ => Task.CompletedTask;

    public Func<UpdatingContext<T>, Task> OnUpdatingAsync { get; set; } = _ => Task.CompletedTask;

    public Func<UpdatedContext<T>, Task> OnUpdatedAsync { get; set; } = _ => Task.CompletedTask;

    public Func<ValidatingContext<T>, Task> OnValidatingAsync { get; set; } = _ => Task.CompletedTask;

    public Func<ValidatedContext<T>, Task> OnValidatedAsync { get; set; } = _ => Task.CompletedTask;

    public Func<InitializingContext<T>, Task> OnInitializingAsync { get; set; } = _ => Task.CompletedTask;

    public Func<InitializedContext<T>, Task> OnInitializedAsync { get; set; } = _ => Task.CompletedTask;

    public Func<LoadedContext<T>, Task> OnLoadedAsync { get; set; } = _ => Task.CompletedTask;

    public Task DeletingAsync(DeletingContext<T> ctx) => OnDeletingAsync(ctx);

    public Task DeletedAsync(DeletedContext<T> ctx) => OnDeletedAsync(ctx);

    public Task CreatingAsync(CreatingContext<T> ctx) => OnCreatingAsync(ctx);

    public Task CreatedAsync(CreatedContext<T> ctx) => OnCreatedAsync(ctx);

    public Task UpdatingAsync(UpdatingContext<T> ctx) => OnUpdatingAsync(ctx);

    public Task UpdatedAsync(UpdatedContext<T> ctx) => OnUpdatedAsync(ctx);

    public Task ValidatingAsync(ValidatingContext<T> ctx) => OnValidatingAsync(ctx);

    public Task ValidatedAsync(ValidatedContext<T> ctx) => OnValidatedAsync(ctx);

    public Task InitializingAsync(InitializingContext<T> ctx) => OnInitializingAsync(ctx);

    public Task InitializedAsync(InitializedContext<T> ctx) => OnInitializedAsync(ctx);

    public Task LoadedAsync(LoadedContext<T> ctx) => OnLoadedAsync(ctx);
}
