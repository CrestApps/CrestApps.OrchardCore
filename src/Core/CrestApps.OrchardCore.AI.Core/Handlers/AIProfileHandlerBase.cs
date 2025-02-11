using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AIProfileHandlerBase : IAIProfileHandler
{
    public virtual Task DeletedAsync(DeletedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedAIProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingAIProfileContext context)
        => Task.CompletedTask;
}
