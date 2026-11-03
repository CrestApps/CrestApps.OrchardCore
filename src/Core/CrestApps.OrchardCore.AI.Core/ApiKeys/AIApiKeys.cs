using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// API keys and scopes for the AI API: making and checking keys, and checking that a caller may use an area of the API.
/// </summary>
public static class AIApiKeys
{
    /// <summary>
    /// The authentication scheme of API keys.
    /// </summary>
    public const string Scheme = "AIApiKey";

    /// <summary>
    /// The schemes the AI API accepts: Orchard Core's API tokens and API keys.
    /// </summary>
    public const string ApiSchemes = OrchardCoreConstants.AuthenticationSchemes.Api + "," + Scheme;

    /// <summary>
    /// The header an API key is sent in. <c>Authorization: Bearer</c> works too.
    /// </summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>
    /// The start of a server key.
    /// </summary>
    public const string ServerKeyStart = "cak_";

    /// <summary>
    /// The start of a browser key.
    /// </summary>
    public const string BrowserKeyStart = "cpk_";

    /// <summary>
    /// The claim that holds a scope. OpenID tokens carry theirs in the same claim type.
    /// </summary>
    public const string ScopeClaimType = "scope";

    /// <summary>
    /// The claim that holds the key's id.
    /// </summary>
    public const string KeyClaimType = "crestapps_api_key";

    /// <summary>
    /// The claim that holds a profile a browser key may chat with.
    /// </summary>
    public const string ProfileClaimType = "crestapps_api_key_profile";

    private const string ScopePrefix = "ai.";

    // OpenIddict keeps the scopes of a validated token in its own claim type.
    private const string OpenIddictScopeClaimType = "oi_scp";

    private const int PrefixLength = 12;

    /// <summary>
    /// Makes a new key.
    /// </summary>
    /// <param name="kind">The kind of key.</param>
    /// <returns>The key, its prefix and its hash.</returns>
    public static (string Key, string Prefix, string Hash) Generate(AIApiKeyKind kind)
    {
        var start = kind == AIApiKeyKind.Browser ? BrowserKeyStart : ServerKeyStart;
        var key = start + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)) + "_" + Base64Url(RandomNumberGenerator.GetBytes(24));

        return (key, GetPrefix(key), Hash(key));
    }

    /// <summary>
    /// Gets the part of a key that finds it.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The prefix, or <see langword="null"/> when it is not a key.</returns>
    public static string GetPrefix(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= PrefixLength ||
            !(key.StartsWith(ServerKeyStart, StringComparison.Ordinal) || key.StartsWith(BrowserKeyStart, StringComparison.Ordinal)))
        {
            return null;
        }

        return key[..PrefixLength];
    }

    /// <summary>
    /// Hashes a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The lowercase hex SHA-256 hash.</returns>
    public static string Hash(string key)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key ?? string.Empty)));

    /// <summary>
    /// Checks a key against the one stored.
    /// </summary>
    /// <param name="stored">The stored key.</param>
    /// <param name="key">The key sent.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>Whether the key works.</returns>
    public static bool Matches(AIApiKey stored, string key, DateTime nowUtc)
    {
        if (stored is null || !stored.Enabled || (stored.ExpiresUtc is { } expires && expires <= nowUtc))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored.Hash ?? string.Empty), Encoding.ASCII.GetBytes(Hash(key)));
    }

    /// <summary>
    /// Checks that a browser key is used from one of its web sites.
    /// </summary>
    /// <param name="stored">The key.</param>
    /// <param name="origin">The request's <c>Origin</c> header.</param>
    /// <returns>Whether the key may be used from there.</returns>
    public static bool AllowsOrigin(AIApiKey stored, string origin)
    {
        if (stored.Kind != AIApiKeyKind.Browser)
        {
            return true;
        }

        if (string.IsNullOrEmpty(origin))
        {
            return false;
        }

        return stored.AllowedOrigins.Any(allowed => string.Equals(allowed?.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the user a key acts as.
    /// </summary>
    /// <param name="stored">The key.</param>
    /// <returns>The user.</returns>
    public static ClaimsPrincipal CreatePrincipal(AIApiKey stored)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "apikey:" + stored.ItemId),
            new(ClaimTypes.Name, stored.Name ?? stored.Prefix ?? "API key"),
            new(KeyClaimType, stored.ItemId ?? string.Empty),
        };

        if (stored.Kind == AIApiKeyKind.Browser)
        {
            // A browser key only chats with its profiles: no roles, no other scope.
            claims.Add(new Claim(ScopeClaimType, ScopePrefix + Scopes.Chat));
            claims.AddRange(stored.AllowedProfileIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => new Claim(ProfileClaimType, id)));
        }
        else
        {
            claims.AddRange(stored.Scopes.Where(Scopes.All.Contains).Select(scope => new Claim(ScopeClaimType, ScopePrefix + scope)));
            claims.AddRange(stored.Roles.Where(role => !string.IsNullOrEmpty(role)).Select(role => new Claim(ClaimTypes.Role, role)));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    /// <summary>
    /// Checks that a caller may use an area of the API and has the permission.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContext">The request.</param>
    /// <param name="scope">The area, from <see cref="Scopes"/>.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="resource">The resource the permission is checked on, if any.</param>
    /// <returns>Whether the caller may go on.</returns>
    public static async Task<bool> AuthorizeAsync(IAuthorizationService authorizationService, HttpContext httpContext, string scope, Permission permission, object resource = null)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(httpContext);

        return AllowsScope(httpContext.User, scope) &&
            await authorizationService.AuthorizeAsync(httpContext.User, permission, resource);
    }

    /// <summary>
    /// Checks that a caller may use an area of the API. A key may use only its scopes. An OpenID token with no
    /// <c>ai.</c> scope may use every area, so existing clients keep working; one with <c>ai.</c> scopes only those.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="scope">The area, from <see cref="Scopes"/>.</param>
    /// <returns>Whether the caller may use it.</returns>
    public static bool AllowsScope(ClaimsPrincipal user, string scope)
    {
        if (user is null)
        {
            return false;
        }

        var scopes = user.Claims
            .Where(claim => claim.Type is ScopeClaimType or OpenIddictScopeClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(value => value.StartsWith(ScopePrefix, StringComparison.Ordinal))
            .ToList();

        if (scopes.Count == 0)
        {
            return !user.HasClaim(claim => claim.Type == KeyClaimType);
        }

        return scopes.Contains(ScopePrefix + scope, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether the caller is a browser key that may chat with a profile.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="profileId">The profile.</param>
    /// <returns>Whether it may.</returns>
    public static bool BrowserKeyAllowsProfile(ClaimsPrincipal user, string profileId)
        => !string.IsNullOrEmpty(profileId) &&
            user?.HasClaim(ProfileClaimType, profileId) == true;

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The areas of the AI API. In OpenID scopes and on keys they read <c>ai.</c> and the area, such as <c>ai.calls</c>.
    /// </summary>
    public static class Scopes
    {
        /// <summary>AI profiles and their versions.</summary>
        public const string Profiles = "profiles";

        /// <summary>Conversations, their analysis and evaluations, and simulations.</summary>
        public const string Conversations = "conversations";

        /// <summary>Test cases and test runs.</summary>
        public const string Tests = "tests";

        /// <summary>Starting, watching and controlling AI calls and texts.</summary>
        public const string Calls = "calls";

        /// <summary>Phone numbers and other channel addresses.</summary>
        public const string Numbers = "numbers";

        /// <summary>Data sources, documents and file sources.</summary>
        public const string Knowledge = "knowledge";

        /// <summary>Contact memory.</summary>
        public const string Contacts = "contacts";

        /// <summary>Chatting with a profile.</summary>
        public const string Chat = "chat";

        /// <summary>Recordings and their links.</summary>
        public const string Recordings = "recordings";

        /// <summary>
        /// Every area.
        /// </summary>
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Profiles, Conversations, Tests, Calls, Numbers, Knowledge, Contacts, Chat, Recordings,
        };
    }
}
