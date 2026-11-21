using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A kind of audience rule, registered by the module that owns the data, such as roles (Users), teams (Teams), hire
/// date (Staff), skill holders (Skills) or course learners (Learning). Register one with
/// <c>services.AddUserAudienceRuleSource&lt;T&gt;()</c> in a startup that requires the Users feature.
/// </summary>
public interface IUserAudienceRuleSource
{
    /// <summary>
    /// Gets the technical name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown in the audience editor.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets what the number of a rule means, or <see langword="null"/> when the source uses no number.
    /// </summary>
    LocalizedString NumberLabel => null;

    /// <summary>
    /// Lists the values a rule can choose, such as the roles of the site; empty when the source takes no values.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The values.</returns>
    Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the users a rule matches.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides whether a rule matches a user.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
        => (await ResolveUserIdsAsync(rule, cancellationToken)).Contains(userId);
}

/// <summary>
/// A value an audience rule can choose.
/// </summary>
/// <param name="Value">The value stored.</param>
/// <param name="Text">The text shown.</param>
public sealed record UserAudienceRuleOption(string Value, string Text);
