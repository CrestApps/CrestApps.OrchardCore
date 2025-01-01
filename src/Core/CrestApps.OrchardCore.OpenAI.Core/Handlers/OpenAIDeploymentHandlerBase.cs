using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public abstract class OpenAIDeploymentHandlerBase : IOpenAIDeploymentHandler
{
    public virtual Task DeletedAsync(DeletedOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedOpenAIDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedModelDeploymentContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingOpenAIDeploymentContext context)
        => Task.CompletedTask;
}
