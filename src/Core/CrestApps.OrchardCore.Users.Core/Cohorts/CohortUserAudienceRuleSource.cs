using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Users.Core.Cohorts;

/// <summary>
/// An audience rule that matches the people in any of some cohorts, so every feature that targets audiences (required
/// learning, layer rules, user pickers...) can target a cohort.
/// </summary>
public sealed class CohortUserAudienceRuleSource : IUserAudienceRuleSource
{
    /// <summary>
    /// The name of the source.
    /// </summary>
    public const string SourceName = "CohortMember";

    private readonly ICohortService _cohorts;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohortUserAudienceRuleSource"/> class.
    /// </summary>
    public CohortUserAudienceRuleSource(ICohortService cohorts, IStringLocalizer<CohortUserAudienceRuleSource> stringLocalizer)
    {
        _cohorts = cohorts;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Cohort member"];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _cohorts.ListAsync(cancellationToken: cancellationToken)).Select(cohort => new UserAudienceRuleOption(cohort.CohortId, cohort.Name)).ToArray();

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cohortId in rule.Values ?? [])
        {
            ids.UnionWith(await _cohorts.ListMemberIdsAsync(cohortId, cancellationToken));
        }

        return ids;
    }

    /// <inheritdoc/>
    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        foreach (var cohortId in rule.Values ?? [])
        {
            if (await _cohorts.IsMemberAsync(cohortId, userId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
