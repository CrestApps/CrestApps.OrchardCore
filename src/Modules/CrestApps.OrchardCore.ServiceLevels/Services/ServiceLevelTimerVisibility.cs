using System.Security.Claims;
using CrestApps.OrchardCore.ServiceLevels.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Decides which service-level timers a user may see, for the Service-level timers report and the AI tools: those who
/// manage policies see every timer; anyone else sees the timers of the records each kind's
/// <see cref="IServiceLevelTimerAccessProvider"/> lets them see, and none of a kind without a provider.
/// </summary>
internal static class ServiceLevelTimerVisibility
{
    /// <summary>
    /// Returns whether a user sees every timer: they manage service level policies.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="user">The user.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    public static Task<bool> SeesEveryTimerAsync(IAuthorizationService authorizationService, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        return user is null
            ? Task.FromResult(false)
            : authorizationService.AuthorizeAsync(user, ServiceLevelsPermissions.ManageServiceLevelPolicies);
    }

    /// <summary>
    /// Keys the access providers by the kind of record they decide for; the first one registered for a kind wins.
    /// </summary>
    /// <param name="providers">The registered providers.</param>
    /// <returns>The providers by kind.</returns>
    public static Dictionary<string, IServiceLevelTimerAccessProvider> ByKind(IEnumerable<IServiceLevelTimerAccessProvider> providers)
        => (providers ?? [])
            .GroupBy(provider => provider.SubjectKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>
    /// Keeps the timers of a batch whose records the user may see, asking each kind's provider once per record, and
    /// empties the batch.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="providers">The providers by kind, from <see cref="ByKind"/>.</param>
    /// <param name="known">What was already decided for each record, by kind and id; filled as records are checked.</param>
    /// <param name="buffer">The batch of timers; it is cleared.</param>
    /// <param name="cancellationToken">A token to cancel the checks.</param>
    /// <returns>The timers the user may see, in the batch's order.</returns>
    public static async Task<List<ServiceLevelTimer>> KeepVisibleAsync(
        ClaimsPrincipal user,
        IReadOnlyDictionary<string, IServiceLevelTimerAccessProvider> providers,
        Dictionary<(string, string), bool> known,
        List<ServiceLevelTimer> buffer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(buffer);

        foreach (var kind in buffer.GroupBy(timer => timer.Subject.Kind ?? string.Empty, StringComparer.Ordinal))
        {
            var unknown = kind.Select(timer => timer.Subject.Id).Where(id => !string.IsNullOrEmpty(id) && !known.ContainsKey((kind.Key, id))).Distinct(StringComparer.Ordinal).ToList();

            if (unknown.Count == 0)
            {
                continue;
            }

            var visible = providers.TryGetValue(kind.Key, out var provider)
                ? await provider.GetVisibleAsync(user, unknown, cancellationToken)
                : null;

            foreach (var id in unknown)
            {
                known[(kind.Key, id)] = visible?.Contains(id) == true;
            }
        }

        var kept = buffer.Where(timer => known.GetValueOrDefault((timer.Subject.Kind ?? string.Empty, timer.Subject.Id ?? string.Empty))).ToList();
        buffer.Clear();

        return kept;
    }
}
