using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// Implementation of GitHub OAuth service for Copilot.
/// </summary>
public sealed class GitHubOAuthService : IGitHubOAuthService
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.GitHubTokens";
    private const string SettingsProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly UserManager<IUser> _userManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubOAuthService> _logger;

    public GitHubOAuthService(
        UserManager<IUser> userManager,
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubOAuthService> logger)
    {
        _userManager = userManager;
        _dataProtectionProvider = dataProtectionProvider;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<CopilotSettings>();

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new InvalidOperationException("GitHub OAuth Client ID is not configured. Please configure Copilot settings.");
        }

        var callbackUrl = settings.CallbackUrl;
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request != null)
            {
                callbackUrl = $"{request.Scheme}://{request.Host}/CopilotAuth/OAuthCallback";
            }
        }

        var scopes = string.Join(" ", settings.Scopes ?? ["user:email", "read:org"]);
        var state = returnUrl ?? string.Empty;

        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["client_id"] = settings.ClientId;
        queryParams["redirect_uri"] = callbackUrl;
        queryParams["scope"] = scopes;
        queryParams["state"] = state;

        return $"https://github.com/login/oauth/authorize?{queryParams}";
    }

    public async Task<GitHubOAuthCredential> ExchangeCodeForTokenAsync(
        string code,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<CopilotSettings>();

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
        {
            throw new InvalidOperationException("GitHub OAuth credentials are not configured. Please configure Copilot settings.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        // Unprotect the client secret
        var settingsProtector = _dataProtectionProvider.CreateProtector(SettingsProtectorPurpose);
        var clientSecret = settingsProtector.Unprotect(settings.ProtectedClientSecret);

        // Exchange authorization code for access token
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CrestApps-OrchardCore-Copilot/1.0");

        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = clientSecret,
            ["code"] = code
        };

        var tokenResponse = await httpClient.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            tokenRequest,
            cancellationToken);

        tokenResponse.EnsureSuccessStatusCode();

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var accessToken = tokenData.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Failed to retrieve access token from GitHub.");
        }

        // Get user information from GitHub
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await httpClient.GetAsync("https://api.github.com/user", cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        var userData = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var username = userData.GetProperty("login").GetString();

        // Protect tokens
        var tokenProtector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        var credentials = new GitHubOAuthCredentials
        {
            GitHubUsername = username,
            ProtectedAccessToken = tokenProtector.Protect(accessToken),
            ProtectedRefreshToken = null, // GitHub OAuth doesn't provide refresh tokens
            ExpiresAt = null, // GitHub tokens don't have explicit expiration
            UpdatedUtc = DateTime.UtcNow
        };

        if (user is User usr)
        {
            usr.Put(credentials);
            await _userManager.UpdateAsync(usr);
        }

        return new GitHubOAuthCredential
        {
            UserId = userId,
            GitHubUsername = username,
            ExpiresAt = null,
            UpdatedUtc = credentials.UpdatedUtc
        };
    }

    public async Task<GitHubOAuthCredential> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var credentials = (user as IEntity)?.As<GitHubOAuthCredentials>();
        if (credentials == null || string.IsNullOrEmpty(credentials.ProtectedAccessToken))
        {
            return null;
        }

        return new GitHubOAuthCredential
        {
            UserId = userId,
            GitHubUsername = credentials.GitHubUsername,
            ExpiresAt = credentials.ExpiresAt,
            UpdatedUtc = credentials.UpdatedUtc
        };
    }

    public async Task<string> GetValidAccessTokenAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var credentials = (user as IEntity)?.As<GitHubOAuthCredentials>();
        if (credentials == null || string.IsNullOrEmpty(credentials.ProtectedAccessToken))
        {
            return null;
        }

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
        
        try
        {
            var accessToken = protector.Unprotect(credentials.ProtectedAccessToken);
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect access token for user {UserId}", userId);
            return null;
        }
    }

    public async Task DisconnectAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        // Clear credentials by setting tokens to null
        if (user is User usr)
        {
            usr.Put(new GitHubOAuthCredentials
            {
                ProtectedAccessToken = null,
                ProtectedRefreshToken = null,
                GitHubUsername = null,
                ExpiresAt = null,
                UpdatedUtc = DateTime.UtcNow
            });

            await _userManager.UpdateAsync(usr);
        }
    }

    public async Task<bool> IsAuthenticatedAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(userId, cancellationToken);
        
        if (credential == null)
        {
            return false;
        }

        // Check if token is not expired
        if (credential.ExpiresAt.HasValue && credential.ExpiresAt.Value < DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }
}
