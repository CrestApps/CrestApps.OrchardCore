using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public abstract class OpenAIChatProfileHandlerBase : IOpenAIChatProfileHandler
{
    public virtual Task DeletedAsync(DeletedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(DeletingOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(InitializedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(InitializingOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(LoadedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavedAsync(SavedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task SavingAsync(SavingOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatedAsync(UpdatedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task UpdatingAsync(UpdatingOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatedAsync(ValidatedOpenAIChatProfileContext context)
        => Task.CompletedTask;

    public virtual Task ValidatingAsync(ValidatingOpenAIChatProfileContext context)
        => Task.CompletedTask;
}
