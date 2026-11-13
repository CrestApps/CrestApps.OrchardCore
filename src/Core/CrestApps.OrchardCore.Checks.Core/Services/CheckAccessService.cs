using System.Security.Claims;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// The default <see cref="ICheckAccessService"/>: asks the source access of each source kind once per request and
/// level, and allows the checks of every other source kind.
/// </summary>
public sealed class CheckAccessService : ICheckAccessService
{
    private readonly IEnumerable<ICheckSourceAccess> _sources;
    private readonly Dictionary<(ClaimsPrincipal User, CheckAccessLevel Level), CheckAccessFilter> _filters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckAccessService"/> class.
    /// </summary>
    /// <param name="sources">The source accesses of the modules that write checks.</param>
    public CheckAccessService(IEnumerable<ICheckSourceAccess> sources)
    {
        _sources = sources;
    }

    /// <inheritdoc />
    public async Task<CheckAccessFilter> GetFilterAsync(ClaimsPrincipal user, CheckAccessLevel level, CancellationToken cancellationToken = default)
    {
        var key = (user, level);

        if (user is not null && _filters.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var restricted = new List<string>();
        var allowed = new List<string>();

        foreach (var source in _sources)
        {
            if (string.IsNullOrEmpty(source.SourceKind))
            {
                continue;
            }

            // Nobody signed in sees nothing a source restricts.
            var scope = user is null
                ? CheckSourceScope.None
                : await source.GetScopeAsync(user, level, cancellationToken) ?? CheckSourceScope.None;

            if (scope.IsAll)
            {
                continue;
            }

            restricted.Add(source.SourceKind);
            allowed.AddRange(scope.Scopes.Select(accessScope => CheckAccessFilter.KeyOf(source.SourceKind, accessScope)));
        }

        var filter = restricted.Count == 0 ? CheckAccessFilter.Unrestricted : new CheckAccessFilter(restricted, allowed);

        if (user is not null)
        {
            _filters[key] = filter;
        }

        return filter;
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, CheckRecord check, CheckAccessLevel level, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        return (await GetFilterAsync(user, level, cancellationToken)).Allows(check);
    }
}
