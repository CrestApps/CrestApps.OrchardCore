using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.Recipes.Events;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Brings the account lists up to date after a recipe defines content types. Orchard Core's content definition step
/// saves the types without raising the definition events the administration raises, so a contact or opportunity type
/// a recipe adds would otherwise never be offered in the account list.
/// </summary>
internal sealed class CrmRecipeEventHandler : IRecipeEventHandler
{
    private readonly CrmAccountListSynchronizer _synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmRecipeEventHandler"/> class.
    /// </summary>
    /// <param name="synchronizer">The account list synchronizer.</param>
    public CrmRecipeEventHandler(CrmAccountListSynchronizer synchronizer)
    {
        _synchronizer = synchronizer;
    }

    public Task RecipeExecutingAsync(string executionId, RecipeDescriptor descriptor)
        => Task.CompletedTask;

    public Task RecipeStepExecutingAsync(RecipeExecutionContext context)
        => Task.CompletedTask;

    public Task RecipeStepExecutedAsync(RecipeExecutionContext context)
    {
        if (context?.Name is "ContentDefinition" or "ReplaceContentDefinition")
        {
            return _synchronizer.SynchronizeAsync();
        }

        return Task.CompletedTask;
    }

    public Task RecipeExecutedAsync(string executionId, RecipeDescriptor descriptor)
        => Task.CompletedTask;

    public Task ExecutionFailedAsync(string executionId, RecipeDescriptor descriptor)
        => Task.CompletedTask;
}
