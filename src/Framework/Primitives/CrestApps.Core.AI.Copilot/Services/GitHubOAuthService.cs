using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using CrestApps.Core.AI.Copilot.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Copilot.Services;

/// <summary>
/// Implementation of GitHub OAuth service for Copilot.
/// </summary>
public sealed class GitHubOAuthService
{
    private const string ProtectorPurpose = "CrestApps.Core.AI.Copilot.GitHubTokens";

    private readonly ICopilotCredentialStore _credentialStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptions<CopilotOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GitHubOAuthService> _logger;

    public GitHubOAuthService(
        ICopilotCredentialStore credentialStore,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<CopilotOptions> options,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger<GitHubOAuthService> logger)
    {
        _credentialStore = credentialStore;
        _dataProtectionProvider = dataProtectionProvider;
        _options = options;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string callbackUrl, string returnUrl)
    {
        var settings = _options.Value;

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new InvalidOperationException("GitHub OAuth Client ID is not configured. Please configure Copilot settings.");
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
        var settings = _options.Value;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new InvalidOperationException("GitHub OAuth credentials are not configured. Please configure Copilot settings.");
        }

        // Exchange authorization code for access token.
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CrestApps-OrchardCore-Copilot/1.0");

        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
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

        // Get user information from GitHub.
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await httpClient.GetAsync("https://api.github.com/user", cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        var userData = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var username = userData.GetProperty("login").GetString();

        // Protect tokens.
        var tokenProtector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var credential = new CopilotProtectedCredential
        {
            GitHubUsername = username,
            ProtectedAccessToken = tokenProtector.Protect(accessToken),
            ProtectedRefreshToken = null, // GitHub OAuth doesn't provide refresh tokens
            ExpiresAt = null, // GitHub tokens don't have explicit expiration
            UpdatedUtc = now,
        };

        await _credentialStore.SaveProtectedCredentialAsync(userId, credential, cancellationToken);

        return new GitHubOAuthCredential
        {
            UserId = userId,
            GitHubUsername = username,
            ExpiresAt = null,
            UpdatedUtc = now,
        };
    }

    public async Task<GitHubOAuthCredential> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialStore.GetProtectedCredentialAsync(userId, cancellationToken);

        if (credential is null || string.IsNullOrEmpty(credential.ProtectedAccessToken))
        {
            return null;
        }

        return new GitHubOAuthCredential
        {
            UserId = userId,
            GitHubUsername = credential.GitHubUsername,
            ExpiresAt = credential.ExpiresAt,
            UpdatedUtc = credential.UpdatedUtc,
        };
    }
    /// <summary>
    /// Gets the raw protected (encrypted) credentials for the specified user.
    /// These can be stored on an AIProfile entity for reuse across sessions.
    /// </summary>
    public async Task<CopilotProtectedCredential> GetProtectedCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialStore.GetProtectedCredentialAsync(userId, cancellationToken);

        if (credential is not null && !string.IsNullOrEmpty(credential.ProtectedAccessToken))
        {
            return credential;
        }

        return null;
    }

    public async Task<string> GetValidAccessTokenAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialStore.GetProtectedCredentialAsync(userId, cancellationToken);

        if (credential is null || string.IsNullOrEmpty(credential.ProtectedAccessToken))
        {
            return null;
        }

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        try
        {
            var accessToken = protector.Unprotect(credential.ProtectedAccessToken);

            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect access token for user {UserId}", userId);

            return null;
        }
    }
    /// <summary>
    /// Unprotects a stored access token from a <see cref="CopilotSessionMetadata"/>,
    /// typically stored on an <see cref="AI.Models.AIProfile"/> entity.
    /// Returns <c>null</c> if the token is missing, expired, or cannot be unprotected.
    /// </summary>
    internal string UnprotectAccessToken(CopilotSessionMetadata metadata)
    {
        if (metadata is null || string.IsNullOrEmpty(metadata.ProtectedAccessToken))
        {
            return null;
        }

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        try
        {
            return protector.Unprotect(metadata.ProtectedAccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect access token from profile metadata for user {Username}", metadata.GitHubUsername);

            return null;
        }
    }

    public async Task DisconnectAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _credentialStore.ClearCredentialAsync(userId, cancellationToken);
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

        // Check if token is not expired.

        if (credential.ExpiresAt.HasValue && credential.ExpiresAt.Value < _timeProvider.GetUtcNow().UtcDateTime)
        {
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyCollection<CopilotModelInfo>> ListModelsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetValidAccessTokenAsync(userId, cancellationToken);

        if (string.IsNullOrEmpty(accessToken))
        {
            return [];
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CrestApps-OrchardCore-Copilot/1.0");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            var response = await httpClient.GetAsync("https://models.github.ai/catalog/models", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to list Copilot models from GitHub API. Status: {StatusCode}",
                    response.StatusCode);

                return [];
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            var models = new List<CopilotModelInfo>();

            // The response could be a direct array or an object wrapping the array (e.g., { "data": [...] }).
            var items = json.ValueKind == JsonValueKind.Array
            ? json.EnumerateArray()
            : json.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array
            ? dataProp.EnumerateArray()
            : default;

            foreach (var item in items)
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var name = item.TryGetProperty("friendly_name", out var fnProp) ? fnProp.GetString() : null;
                name ??= item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : id;

                if (!string.IsNullOrEmpty(id))
                {
                    models.Add(new CopilotModelInfo { Id = id, Name = name ?? id });
                }
            }

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing Copilot models for user {UserId}", userId);

            return [];
        }
    }
}
