using System.Security.Claims;
using CrestApps.Core.Security;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// An in-memory <see cref="IUserDirectory"/> holding a fixed set of users.
/// </summary>
/// <remarks>
/// Names are indexed separately from the summaries because a host normalizes a name before matching
/// it, and the normalized form is not always derivable from the name the summary carries.
/// </remarks>
internal sealed class FakeUserDirectory : IUserDirectory
{
    private readonly Dictionary<string, UserSummary> _byId;
    private readonly Dictionary<string, UserSummary> _byName = new(StringComparer.OrdinalIgnoreCase);

    public FakeUserDirectory(params UserSummary[] users)
    {
        _byId = (users ?? []).ToDictionary(user => user.Id, StringComparer.Ordinal);

        foreach (var user in users ?? [])
        {
            _byName[user.UserName] = user;
        }
    }

    /// <summary>
    /// Gets or sets the user a principal resolves to, whatever the principal says.
    /// </summary>
    public UserSummary CurrentUser { get; set; }

    /// <summary>
    /// Makes a user findable under a name that is not its own, standing in for a host that matches
    /// on a normalized form.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <param name="user">The user that name resolves to.</param>
    /// <returns>This directory, so calls can be chained.</returns>
    public FakeUserDirectory MapName(string name, UserSummary user)
    {
        _byName[name] = user;
        _byId[user.Id] = user;

        return this;
    }

    public Task<UserSummary> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(userId is not null && _byId.TryGetValue(userId, out var user) ? user : null);

    public Task<UserSummary> FindByNameAsync(string userName, CancellationToken cancellationToken = default)
        => Task.FromResult(userName is not null && _byName.TryGetValue(userName, out var user) ? user : null);

    public Task<UserSummary> FindByPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        => Task.FromResult(principal?.Identity?.IsAuthenticated == true ? CurrentUser : null);

    public Task<IReadOnlyCollection<UserSummary>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<UserSummary>>(
            [.. (userIds ?? []).Where(id => id is not null && _byId.ContainsKey(id)).Select(id => _byId[id])]);
}
