using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Brings the opportunity's primary contact field up to date after a recipe defines content types.
/// </summary>
/// <remarks>
/// Orchard Core's content definition step saves the types without raising the definition events the administration
/// raises, and a recipe event handler misses the step when the same recipe enables the CRM, because the recipe's event
/// handlers are resolved before its first step. Step handlers are resolved for each step, so this one runs at the end
/// of every content definition step, once the step has saved the types.
/// </remarks>
internal sealed class CrmRecipeStepHandler : IRecipeStepHandler
{
    public Task ExecuteAsync(RecipeExecutionContext context)
    {
        if (context?.Name is "ContentDefinition" or "ReplaceContentDefinition" && ShellScope.Current is not null)
        {
            DeferredWork.Add("omnichannel_opportunity_contact_sync", scope => scope.ServiceProvider
                .GetRequiredService<OpportunityContactFieldSynchronizer>()
                .SynchronizeAsync());
        }

        return Task.CompletedTask;
    }
}
