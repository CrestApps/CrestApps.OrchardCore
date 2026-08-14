using CrestApps.Core;
using CrestApps.Core.AI.Copilot;
using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// Bridges the framework-level <see cref="ICopilotCredentialStore"/> to OrchardCore's
/// user model, storing credentials on the User entity via OrchardCore entity metadata.
/// </summary>
internal sealed class OrchardCoreCopilotCredentialStore : ICopilotCredentialStore
{
    private readonly IClock _clock;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreCopilotCredentialStore"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="userManager">The user manager.</param>
    public OrchardCoreCopilotCredentialStore(
        IClock clock,
        UserManager<IUser> userManager)
    {
        _clock = clock;
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves the protected credential async.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<CopilotProtectedCredential> GetProtectedCredentialAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not User usr)
        {
            return null;
        }

        if (!usr.TryGet<GitHubOAuthCredentials>(out var creds) || string.IsNullOrEmpty(creds.ProtectedAccessToken))
        {
            return null;
        }

        return new CopilotProtectedCredential
        {
            GitHubUsername = creds.GitHubUsername,
            ProtectedAccessToken = creds.ProtectedAccessToken,
            ProtectedRefreshToken = creds.ProtectedRefreshToken,
            ExpiresAt = creds.ExpiresAt,
            UpdatedUtc = creds.UpdatedUtc,
        };
    }

    /// <summary>
    /// Saves the protected credential async.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SaveProtectedCredentialAsync(
        string userId, CopilotProtectedCredential credential, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not User usr)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        usr.Put(new GitHubOAuthCredentials
        {
            GitHubUsername = credential.GitHubUsername,
            ProtectedAccessToken = credential.ProtectedAccessToken,
            ProtectedRefreshToken = credential.ProtectedRefreshToken,
            ExpiresAt = credential.ExpiresAt,
            UpdatedUtc = credential.UpdatedUtc ?? _clock.UtcNow,
        });

        await _userManager.UpdateAsync(usr);
    }

    /// <summary>
    /// Asynchronously performs the clear credential operation.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task ClearCredentialAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not User usr)
        {
            return;
        }

        usr.Put(new GitHubOAuthCredentials
        {
            ProtectedAccessToken = null,
            ProtectedRefreshToken = null,
            GitHubUsername = null,
            ExpiresAt = null,
            UpdatedUtc = _clock.UtcNow,
        });

        await _userManager.UpdateAsync(usr);
    }
}
