using CrestApps.AI.Copilot;
using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// Bridges the framework-level <see cref="ICopilotCredentialStore"/> to OrchardCore's
/// user model, storing credentials on the User entity via <c>.As&lt;T&gt;</c>/<c>.Put&lt;T&gt;</c>.
/// </summary>
internal sealed class OrchardCoreCopilotCredentialStore : ICopilotCredentialStore
{
    private readonly UserManager<IUser> _userManager;

    public OrchardCoreCopilotCredentialStore(UserManager<IUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<CopilotProtectedCredential> GetProtectedCredentialAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not User usr)
        {
            return null;
        }

        var creds = usr.As<GitHubOAuthCredentials>();

        if (creds is null || string.IsNullOrEmpty(creds.ProtectedAccessToken))
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
            UpdatedUtc = credential.UpdatedUtc ?? DateTime.UtcNow,
        });

        await _userManager.UpdateAsync(usr);
    }

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
            UpdatedUtc = DateTime.UtcNow,
        });

        await _userManager.UpdateAsync(usr);
    }
}
