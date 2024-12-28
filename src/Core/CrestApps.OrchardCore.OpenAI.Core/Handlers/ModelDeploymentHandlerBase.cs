using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public abstract class ModelDeploymentHandlerBase : IModelDeploymentHandler
{
    public virtual Task DeletedAsync(DeletedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingModelDeploymentContext context)
        => Task.CompletedTask;
}
