using System.Security.Claims;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Core.SourceAccess;

/// <summary>
/// The default <see cref="ISourceAccessService"/>: asks the source access of each source kind once per request, person
/// and level, and allows the records of every other source kind.
/// </summary>
public sealed class SourceAccessService : ISourceAccessService
{
    private readonly IEnumerable<ISourceAccess> _sources;
    private readonly Dictionary<(ClaimsPrincipal User, SourceAccessLevel Level), SourceAccessFilter> _filters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceAccessService"/> class.
    /// </summary>
    /// <param name="sources">The source accesses of the modules that write records.</param>
    public SourceAccessService(IEnumerable<ISourceAccess> sources)
    {
        _sources = sources;
    }

    /// <inheritdoc />
    public async Task<SourceAccessFilter> GetFilterAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
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

            // Without a signed-in person, nothing a source restricts is allowed.
            var scope = user?.Identity?.IsAuthenticated == true
                ? await source.GetScopeAsync(user, level, cancellationToken) ?? SourceAccessScope.None
                : SourceAccessScope.None;

            if (scope.IsAll)
            {
                continue;
            }

            restricted.Add(source.SourceKind);
            allowed.AddRange(scope.Scopes.Select(accessScope => SourceAccessFilter.KeyOf(source.SourceKind, accessScope)));
        }

        var filter = restricted.Count == 0 ? SourceAccessFilter.Unrestricted : new SourceAccessFilter(restricted, allowed);

        if (user is not null)
        {
            _filters[key] = filter;
        }

        return filter;
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string sourceKind, string accessScope, SourceAccessLevel level, CancellationToken cancellationToken = default)
        => (await GetFilterAsync(user, level, cancellationToken)).Allows(sourceKind, accessScope);
}
