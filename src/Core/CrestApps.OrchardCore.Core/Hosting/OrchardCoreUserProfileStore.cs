using CrestApps.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Stores the suite's data on the signed-in Orchard Core user.
/// </summary>
public sealed class OrchardCoreUserProfileStore : IUserProfileStore
{
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreUserProfileStore"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="session">The ambient session, used to evict a stale copy of the current user.</param>
    /// <param name="logger">The logger.</param>
    public OrchardCoreUserProfileStore(
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ISession session,
        ILogger<OrchardCoreUserProfileStore> logger)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var user = await GetCurrentUserAsync();

        return user is IEntity entity && entity.TryGet<T>(out var value) ? value : null;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync<T>(Func<T, bool> mutate, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (await GetCurrentUserAsync() is null)
        {
            throw new UserPersistenceException("There is no current user whose profile could be updated.");
        }

        // Committed on an isolated child scope with its own session. A serialized credential refresh
        // and a disconnect both need the write visible to other requests before the refresh lock is
        // released or the provider is called, and that must not flush unrelated changes the ambient
        // request has staged.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            var principal = httpContextAccessor.HttpContext?.User;

            var user = principal?.Identity?.IsAuthenticated == true
                ? await userManager.GetUserAsync(principal)
                : null;

            if (user is not IEntity entity)
            {
                throw new UserPersistenceException("There is no current user whose profile could be updated.");
            }

            var value = entity.TryGet<T>(out var stored) ? stored : new T();

            if (!mutate(value))
            {
                return;
            }

            entity.Put(value);

            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));

                _logger.LogError("Unable to update the current user's profile. {Errors}", errors);

                throw new UserPersistenceException("The current user's profile could not be committed.");
            }
        });
    }

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync();

        if (user is not null)
        {
            // Detaching drops the request's cached copy, so the next read sees what a peer committed
            // on its own scope rather than the version loaded earlier in this request.
            _session.Detach(user);
        }
    }

    /// <summary>
    /// Gets the signed-in user.
    /// </summary>
    /// <returns>The user, or <see langword="null"/> when nobody is signed in.</returns>
    private async Task<IUser> GetCurrentUserAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal?.Identity?.IsAuthenticated == true
            ? await _userManager.GetUserAsync(principal)
            : null;
    }
}
