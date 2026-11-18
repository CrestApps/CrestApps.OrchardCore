using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every ticket board into a deployment plan's recipe, a personal one with its owner's user name.
/// </summary>
internal sealed class TicketBoardsDeploymentSource : TicketSharedRecordsDeploymentSource<TicketBoardsDeploymentStep, TicketBoard>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBoardsDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    /// <param name="users">The users.</param>
    public TicketBoardsDeploymentSource(ICatalogManager<TicketBoard> manager, UserManager<IUser> users)
        : base(TicketBoardsRecipeStep.EntriesName, manager, users)
    {
    }
}
