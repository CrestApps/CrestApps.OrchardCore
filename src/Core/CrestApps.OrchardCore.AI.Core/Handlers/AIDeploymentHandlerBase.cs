using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AIDeploymentHandlerBase : IAIDeploymentHandler
{
    public virtual Task DeletedAsync(DeletedAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingAIDeploymentContext context)
        => Task.CompletedTask;
}
