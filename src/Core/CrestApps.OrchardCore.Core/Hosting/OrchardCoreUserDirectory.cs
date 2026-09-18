using System.Security.Claims;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Projects Orchard Core's users into the summaries the Contact Center Suite reads.
/// </summary>
public sealed class OrchardCoreUserDirectory : IUserDirectory
{
    /// <summary>
    /// How many identifiers a single batch lookup asks for.
    /// </summary>
    /// <remarks>
    /// Bounded because the identifiers end up in an <c>IN</c> clause, and every database has a limit
    /// on how many parameters one statement may carry.
    /// </remarks>
    private const int BatchSize = 500;

    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreUserDirectory"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="session">The ambient session, used to look several users up at once.</param>
    /// <param name="displayNameProviders">
    /// The registered display-name providers, if any. Taken as a sequence deliberately: only the
    /// CrestApps Users feature registers one, and a Contact Center or Telephony tenant that does not
    /// enable it must still resolve agents rather than fail at request time.
    /// </param>
    public OrchardCoreUserDirectory(
        UserManager<IUser> userManager,
        ISession session,
        IEnumerable<IDisplayNameProvider> displayNameProviders)
    {
        _userManager = userManager;
        _session = session;
        _displayNameProvider = displayNameProviders?.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<UserSummary> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? null
            : await ProjectAsync(await _userManager.FindByIdAsync(userId), cancellationToken);

    /// <inheritdoc/>
    public async Task<UserSummary> FindByNameAsync(string userName, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userName)
            ? null
            : await ProjectAsync(await _userManager.FindByNameAsync(userName), cancellationToken);

    /// <inheritdoc/>
    public async Task<UserSummary> FindByPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        => principal?.Identity?.IsAuthenticated == true
            ? await ProjectAsync(await _userManager.GetUserAsync(principal), cancellationToken)
            : null;

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSummary>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var summaries = new List<UserSummary>(ids.Length);

        // Queried in bounded batches rather than one lookup per identifier: the callers here are
        // rendering a roster, and a per-row round trip is what this member exists to avoid.
        foreach (var batch in ids.Chunk(BatchSize))
        {
            var users = await _session
                .Query<User, UserIndex>(index => index.UserId.IsIn(batch))
                .ListAsync(cancellationToken);

            foreach (var user in users)
            {
                summaries.Add(await ProjectAsync(user, cancellationToken));
            }
        }

        return summaries;
    }

    /// <summary>
    /// Projects an Orchard user into a summary.
    /// </summary>
    /// <param name="user">The user, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary, or <see langword="null"/>.</returns>
    private async Task<UserSummary> ProjectAsync(IUser user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        var userName = await _userManager.GetUserNameAsync(user);
        var displayName = _displayNameProvider is null
            ? null
            : await _displayNameProvider.GetAsync(user, cancellationToken);

        return new UserSummary(
            await _userManager.GetUserIdAsync(user),
            userName,
            // The sign-in name is the fallback rather than an empty string, because every caller of
            // this is putting the result on screen next to a call.
            string.IsNullOrWhiteSpace(displayName) ? userName : displayName,
            await _userManager.GetEmailAsync(user));
    }
}
