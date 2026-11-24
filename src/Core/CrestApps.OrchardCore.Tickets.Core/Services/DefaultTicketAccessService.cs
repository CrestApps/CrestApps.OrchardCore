using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The default <see cref="ITicketAccessService"/>: the per-desk permissions, with the view scope of each desk, and the
/// user's teams from the Teams building block.
/// </summary>
public sealed class DefaultTicketAccessService : ITicketAccessService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IServiceProvider _serviceProvider;

    private readonly Dictionary<ClaimsPrincipal, IReadOnlyDictionary<string, TicketViewScope>> _scopes = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _teams = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTicketAccessService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="serviceProvider">The services, for the optional team membership.</param>
    public DefaultTicketAccessService(IAuthorizationService authorizationService, ICatalog<TicketDesk> desks, IServiceProvider serviceProvider)
    {
        _authorizationService = authorizationService;
        _desks = desks;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> CanAsync(ClaimsPrincipal user, ContentItem ticket, TicketOperation operation, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || ticket is null || !ticket.TryGet<TicketPart>(out var part) || string.IsNullOrEmpty(part.DeskId))
        {
            return false;
        }

        var scopes = await GetViewScopesAsync(user, cancellationToken);

        if (!scopes.TryGetValue(part.DeskId, out var scope) || !await InScopeAsync(user, part, scope, cancellationToken))
        {
            return false;
        }

        if (operation is TicketOperation.ViewAll or TicketOperation.ViewTeam or TicketOperation.ViewAssigned)
        {
            return true;
        }

        var desk = await _desks.FindByIdAsync(part.DeskId, cancellationToken);

        return desk is not null && await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ForDesk(operation, desk));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, TicketViewScope>> GetViewScopesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new Dictionary<string, TicketViewScope>();
        }

        if (_scopes.TryGetValue(user, out var cached))
        {
            return cached;
        }

        var scopes = new Dictionary<string, TicketViewScope>(StringComparer.Ordinal);

        foreach (var desk in await _desks.GetAllAsync(cancellationToken))
        {
            if (await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.ViewAll, desk)))
            {
                scopes[desk.ItemId] = TicketViewScope.All;
            }
            else if (await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.ViewTeam, desk)))
            {
                scopes[desk.ItemId] = TicketViewScope.Teams;
            }
            else if (await _authorizationService.AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.ViewAssigned, desk)))
            {
                scopes[desk.ItemId] = TicketViewScope.Assigned;
            }
        }

        _scopes[user] = scopes;

        return scopes;
    }

    /// <inheritdoc/>
    public async Task<TicketScopedQuery> ApplyScopeAsync(ClaimsPrincipal user, TicketQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scopes = await GetViewScopesAsync(user, cancellationToken);

        if (scopes.Count == 0)
        {
            return null;
        }

        var userId = UserIdOf(user);

        return new TicketScopedQuery
        {
            Query = query,
            AllDeskIds = [.. scopes.Where(scope => scope.Value == TicketViewScope.All).Select(scope => scope.Key)],
            TeamDeskIds = [.. scopes.Where(scope => scope.Value == TicketViewScope.Teams).Select(scope => scope.Key)],
            AssignedDeskIds = [.. scopes.Where(scope => scope.Value == TicketViewScope.Assigned).Select(scope => scope.Key)],
            TeamIds = scopes.Values.Any(scope => scope == TicketViewScope.Teams) ? await TeamsOfAsync(userId, cancellationToken) : [],
            UserId = userId,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> CanBeAssignedAsync(string userId, string deskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(deskId))
        {
            return false;
        }

        var users = _serviceProvider.GetService<UserManager<IUser>>();
        var principals = _serviceProvider.GetService<IUserClaimsPrincipalFactory<IUser>>();

        // Without the user services the site can't tell; the assignment stands as before.
        if (users is null || principals is null)
        {
            return true;
        }

        if (await users.FindByIdAsync(userId) is not { } user)
        {
            return false;
        }

        var principal = await principals.CreateAsync(user);

        return (await GetViewScopesAsync(principal, cancellationToken)).ContainsKey(deskId);
    }

    private async Task<bool> InScopeAsync(ClaimsPrincipal user, TicketPart part, TicketViewScope scope, CancellationToken cancellationToken)
    {
        var userId = UserIdOf(user);

        return scope switch
        {
            TicketViewScope.All => true,
            // A principal without a user id (an API token for a client) is never the assignee, even of an unassigned ticket.
            TicketViewScope.Teams => (!string.IsNullOrEmpty(userId) && part.AssigneeId == userId) || (!string.IsNullOrEmpty(part.TeamId) && (await TeamsOfAsync(userId, cancellationToken)).Contains(part.TeamId)),
            TicketViewScope.Assigned => !string.IsNullOrEmpty(userId) && part.AssigneeId == userId,
            _ => false,
        };
    }

    private async Task<IReadOnlyList<string>> TeamsOfAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || _serviceProvider.GetService<ITeamMembershipService>() is not { } memberships)
        {
            return [];
        }

        if (!_teams.TryGetValue(userId, out var teams))
        {
            teams = [.. (await memberships.GetMembershipsAsync(userId, cancellationToken: cancellationToken)).Select(member => member.TeamId).Distinct(StringComparer.Ordinal)];
            _teams[userId] = teams;
        }

        return teams;
    }

    private static string UserIdOf(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier);
}
