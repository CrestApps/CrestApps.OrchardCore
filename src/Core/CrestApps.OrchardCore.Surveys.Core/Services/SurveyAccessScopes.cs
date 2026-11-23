using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Works out, at once, which surveys a user may do an operation on, by the rules of <see cref="SurveyAuthorizationService"/>:
/// the global permission grants every survey; the own permission grants the surveys the user owns; access providers grant
/// the rest. Owned surveys are read from the survey index and providers list their grants
/// (<see cref="ISurveyAccessProvider.ListGrantsAsync"/>), so no survey document is loaded; only a provider that can't list
/// its grants is asked about each survey, read from the index.
/// </summary>
public sealed class SurveyAccessScopes
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyAccessProvider> _accessProviders;
    private readonly SurveyIndexReader _reader;

    // A request lists the data sets, then reads one, and each asks: the scope of a principal is worked out once per scope.
    private readonly Dictionary<(ClaimsPrincipal User, SurveyOperation Operation), SurveyAccessScope> _resolved = new(new PrincipalComparer());

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAccessScopes"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessProviders">The access providers.</param>
    /// <param name="reader">Reads surveys from the survey index.</param>
    public SurveyAccessScopes(
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyAccessProvider> accessProviders,
        SurveyIndexReader reader)
    {
        _authorizationService = authorizationService;
        _accessProviders = accessProviders;
        _reader = reader;
    }

    /// <summary>
    /// Works out the surveys a user may do an operation on.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scope.</returns>
    public async Task<SurveyAccessScope> ResolveAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            return SurveyAccessScope.None;
        }

        if (_resolved.TryGetValue((user, operation), out var resolved))
        {
            return resolved;
        }

        return _resolved[(user, operation)] = await ResolveCoreAsync(user, operation, cancellationToken);
    }

    private async Task<SurveyAccessScope> ResolveCoreAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken)
    {
        var (global, own) = SurveyAuthorizationService.PermissionsOf(operation);

        if (await _authorizationService.AuthorizeAsync(user, global))
        {
            return SurveyAccessScope.Everything;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (own is not null && !string.IsNullOrEmpty(userId) && await _authorizationService.AuthorizeAsync(user, own))
        {
            ids.UnionWith(await _reader.ListIdsOwnedByAsync(userId, cancellationToken));
        }

        var unlisted = new List<ISurveyAccessProvider>();

        foreach (var provider in _accessProviders)
        {
            var grants = await provider.ListGrantsAsync(user, operation, cancellationToken);

            if (grants is null)
            {
                unlisted.Add(provider);

                continue;
            }

            ids.UnionWith((grants.SurveyIds ?? []).Where(id => !string.IsNullOrEmpty(id)));
            ids.UnionWith(await _reader.ListIdsByOwnersAsync(grants.Owners ?? [], cancellationToken));
        }

        if (unlisted.Count > 0)
        {
            // A provider that can't list its grants is asked about each survey not granted yet, read from the index.
            foreach (var survey in await _reader.ListAsync(null, publishedOnly: false, cancellationToken))
            {
                if (ids.Contains(survey.ItemId))
                {
                    continue;
                }

                foreach (var provider in unlisted)
                {
                    if (await provider.GrantsAsync(user, survey, operation, cancellationToken))
                    {
                        ids.Add(survey.ItemId);

                        break;
                    }
                }
            }
        }

        return new SurveyAccessScope { SurveyIds = ids };
    }

    private sealed class PrincipalComparer : IEqualityComparer<(ClaimsPrincipal User, SurveyOperation Operation)>
    {
        public bool Equals((ClaimsPrincipal User, SurveyOperation Operation) x, (ClaimsPrincipal User, SurveyOperation Operation) y)
            => ReferenceEquals(x.User, y.User) && x.Operation == y.Operation;

        public int GetHashCode((ClaimsPrincipal User, SurveyOperation Operation) obj)
            => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.User), obj.Operation);
    }
}
