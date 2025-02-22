using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AIToolInstanceHandlerBase : IAIToolInstanceHandler
{
    public virtual Task DeletedAsync(DeletedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedAIToolInstanceContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingAIToolInstanceContext context)
        => Task.CompletedTask;
}
