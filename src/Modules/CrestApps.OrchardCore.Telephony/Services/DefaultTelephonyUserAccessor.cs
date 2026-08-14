using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyUserAccessor"/> that resolves the current user through the
/// <see cref="UserManager{TUser}"/> and the HTTP context.
/// </summary>
public sealed class DefaultTelephonyUserAccessor : ITelephonyUserAccessor
{
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyUserAccessor"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="session">The ambient persistence session used to reload the current user.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyUserAccessor(
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ISession session,
        ILogger<DefaultTelephonyUserAccessor> logger)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IUser> GetCurrentUserAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return await _userManager.GetUserAsync(principal);
    }

    /// <inheritdoc/>
    public async Task<IUser> ReloadCurrentUserAsync()
    {
        var user = await GetCurrentUserAsync();

        if (user is null)
        {
            return null;
        }

        // Evict the tracked instance so the next query reads the row from the database rather than returning
        // this request's earlier copy, which would hide a peer's committed token refresh.
        _session.Detach(user);

        return await GetCurrentUserAsync();
    }

    /// <inheritdoc/>
    public async Task PersistCurrentUserAsync(Func<IUser, bool> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        var current = await GetCurrentUserAsync();

        if (current is null)
        {
            throw new TelephonyUserPersistenceException("There is no current user to persist telephony tokens for.");
        }

        // Persist the change on an isolated child scope with its own session, so the durable commit that a
        // serialized token refresh and a disconnect both need (before releasing the refresh lock or calling
        // the provider) only commits this user document and never flushes unrelated changes the ambient
        // request has staged.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            var principal = httpContextAccessor.HttpContext?.User;

            var user = principal?.Identity?.IsAuthenticated == true
                ? await userManager.GetUserAsync(principal)
                : null;

            if (user is null)
            {
                throw new TelephonyUserPersistenceException("There is no current user to persist telephony tokens for.");
            }

            if (!mutate(user))
            {
                return;
            }

            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var codes = string.Join(", ", result.Errors.Select(error => error.Code));

                _logger.LogError(
                    "Failed to persist telephony token changes for the current user. Identity error codes: {ErrorCodes}",
                    codes);

                throw new TelephonyUserPersistenceException(
                    $"Telephony token changes could not be persisted (identity error codes: {codes}).");
            }

            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            try
            {
                await session.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to commit telephony token changes for the current user.");

                throw new TelephonyUserPersistenceException(
                    "Telephony token changes could not be committed.",
                    exception);
            }
        });

        // Evict the ambient copy so a later read in this request re-reads the committed document.
        _session.Detach(current);
    }
}

