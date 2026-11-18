using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Deployment;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every record of a kind that can be personal (a saved view, a board) into a deployment plan's recipe. A
/// personal record carries its owner's user name (<see cref="ITicketSharedRecord.OwnerUserNameProperty"/>), which the
/// import turns into the user of the target site, so it stays personal instead of becoming one everybody shares.
/// </summary>
/// <typeparam name="TStep">The deployment step.</typeparam>
/// <typeparam name="T">The record.</typeparam>
internal abstract class TicketSharedRecordsDeploymentSource<TStep, T> : CatalogDeploymentSource<TStep, T>
    where TStep : DeploymentStep
    where T : CatalogItem, ITicketSharedRecord
{
    private readonly UserManager<IUser> _users;
    private readonly Dictionary<string, string> _userNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSharedRecordsDeploymentSource{TStep, T}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the array the records are written to.</param>
    /// <param name="manager">The records.</param>
    /// <param name="users">The users.</param>
    protected TicketSharedRecordsDeploymentSource(string entriesName, ICatalogManager<T> manager, UserManager<IUser> users)
        : base(entriesName, manager)
    {
        _users = users;
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<T>> GetEntriesAsync()
    {
        var records = (await base.GetEntriesAsync()).ToList();

        foreach (var ownerId in records.Select(record => record.OwnerId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _users.FindByIdAsync(ownerId) is { } user)
            {
                _userNames[ownerId] = await _users.GetUserNameAsync(user);
            }
        }

        return records;
    }

    /// <inheritdoc/>
    protected override JsonObject Export(T entry)
    {
        var data = base.Export(entry);

        if (!string.IsNullOrEmpty(entry.OwnerId) && _userNames.TryGetValue(entry.OwnerId, out var userName))
        {
            data[ITicketSharedRecord.OwnerUserNameProperty] = userName;
        }

        return data;
    }
}
