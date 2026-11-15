using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every ticket view into a deployment plan's recipe. A personal view carries its owner's user name
/// (<see cref="TicketView.OwnerUserNameProperty"/>), which the import turns into the user of the target site, so it
/// stays personal instead of becoming a view everybody shares.
/// </summary>
internal sealed class TicketViewsDeploymentSource : CatalogDeploymentSource<TicketViewsDeploymentStep, TicketView>
{
    private readonly UserManager<IUser> _users;
    private readonly Dictionary<string, string> _userNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewsDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    /// <param name="users">The users.</param>
    public TicketViewsDeploymentSource(ICatalogManager<TicketView> manager, UserManager<IUser> users)
        : base(TicketViewsRecipeStep.EntriesName, manager)
    {
        _users = users;
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<TicketView>> GetEntriesAsync()
    {
        var views = (await base.GetEntriesAsync()).ToList();

        foreach (var ownerId in views.Select(view => view.OwnerId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _users.FindByIdAsync(ownerId) is { } user)
            {
                _userNames[ownerId] = await _users.GetUserNameAsync(user);
            }
        }

        return views;
    }

    /// <inheritdoc/>
    protected override JsonObject Export(TicketView entry)
    {
        var data = base.Export(entry);

        if (!string.IsNullOrEmpty(entry.OwnerId) && _userNames.TryGetValue(entry.OwnerId, out var userName))
        {
            data[TicketView.OwnerUserNameProperty] = userName;
        }

        return data;
    }
}
