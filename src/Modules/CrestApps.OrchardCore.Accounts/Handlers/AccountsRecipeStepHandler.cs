using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Accounts.Handlers;

/// <summary>
/// Brings the account lists up to date after a recipe defines content types.
/// </summary>
/// <remarks>
/// Orchard Core's content definition step saves the types without raising the definition events the administration
/// raises. A recipe event handler can't catch it either when the same recipe enables the feature that holds the
/// handler, because the recipe's event handlers are resolved before its first step. Step handlers are resolved for each
/// step from the tenant as it is then, so this one sees every step; it waits for the end of the step, when the step's
/// own handler has saved the types.
/// </remarks>
internal sealed class AccountsRecipeStepHandler : IRecipeStepHandler
{
    public Task ExecuteAsync(RecipeExecutionContext context)
    {
        if (context?.Name is "ContentDefinition" or "ReplaceContentDefinition" && ShellScope.Current is not null)
        {
            DeferredWork.Add("account_list_sync", scope => scope.ServiceProvider
                .GetRequiredService<AccountListSynchronizer>()
                .SynchronizeAsync());
        }

        return Task.CompletedTask;
    }
}
