using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AIChatProfileHandlerBase : IAIChatProfileHandler
{
    public virtual Task DeletedAsync(DeletedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingAIChatProfileContext context)
        => Task.CompletedTask;
}
