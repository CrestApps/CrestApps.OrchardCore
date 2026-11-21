namespace CrestApps.OrchardCore.Users;

/// <summary>
/// The audiences of the site, the ones modules register (<see cref="IUserAudienceProvider"/>) and the ones
/// administrators define (<see cref="UserAudienceDefinition"/>), and who is in each one.
/// </summary>
public interface IUserAudienceService
{
    /// <summary>
    /// Lists every audience.
    /// </summary>
    Task<IReadOnlyList<UserAudienceSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an audience an administrator defined.
    /// </summary>
    Task<UserAudienceDefinition> FindDefinitionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates an audience an administrator defines.
    /// </summary>
    /// <returns>The reasons it was refused; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SaveDefinitionAsync(UserAudienceDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an audience an administrator defined.
    /// </summary>
    Task<bool> DeleteDefinitionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the members of an audience, by user id.
    /// </summary>
    /// <param name="name">The audience.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many members there are, and the page of user ids.</returns>
    Task<(int Count, IReadOnlyList<string> UserIds)> GetMembersAsync(string name, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides whether a user is in an audience.
    /// </summary>
    Task<bool> IsMemberAsync(string name, string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// An audience of the site.
/// </summary>
/// <param name="Name">The technical name.</param>
/// <param name="DisplayName">The name shown.</param>
/// <param name="IsDefined">Whether an administrator defined it, rather than a module.</param>
public sealed record UserAudienceSummary(string Name, string DisplayName, bool IsDefined);
