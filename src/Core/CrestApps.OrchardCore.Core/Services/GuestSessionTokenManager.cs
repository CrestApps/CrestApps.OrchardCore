using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Issues and verifies the secret that proves a guest owns a pending session, and carries it in a
/// data-protected cookie.
/// </summary>
/// <remarks>
/// The obvious way to recognize a returning guest is their IP address and user agent, and it does not work.
/// Everyone behind one office router, one mobile carrier NAT, or one corporate proxy shares an address, and
/// a browser's user agent is neither secret nor unique, so that pair identifies a population rather than a
/// person. Worse, both are supplied by the caller, so anyone who can observe or guess them can resume a
/// stranger's checkout and read the contact details and amounts on it.
///
/// A high-entropy random token fixes that: only the browser that started the session is ever given it. Only
/// its SHA-256 hash is stored, so a leaked database does not hand an attacker the ability to resume live
/// sessions, and comparison is done in fixed time so the hash cannot be recovered a byte at a time.
/// </remarks>
public sealed class GuestSessionTokenManager
{
    private const int TokenByteLength = 32;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuestSessionTokenManager"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read and write the cookie.</param>
    /// <param name="dataProtectionProvider">The provider used to encrypt and integrity-protect the cookie.</param>
    public GuestSessionTokenManager(
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Creates a new ownership token for a session, stores it in the visitor's cookie, and returns the hash
    /// to persist on the session itself.
    /// </summary>
    /// <param name="scope">The cookie scope, which keeps one feature's tokens out of another's cookie.</param>
    /// <param name="sessionId">The session the token proves ownership of.</param>
    /// <returns>The SHA-256 hash of the issued token, or <see langword="null"/> when there is no request to
    /// write a cookie to.</returns>
    public string Issue(GuestSessionTokenScope scope, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return null;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteLength));

        var tokens = ReadTokens(scope, httpContext);

        tokens[sessionId] = token;

        WriteTokens(scope, httpContext, tokens);

        return Hash(token);
    }

    /// <summary>
    /// Returns whether the current visitor holds the token that matches the stored hash.
    /// </summary>
    /// <param name="scope">The cookie scope.</param>
    /// <param name="sessionId">The session being resumed.</param>
    /// <param name="storedHash">The hash stored on the session.</param>
    public bool Verify(GuestSessionTokenScope scope, string sessionId, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(storedHash))
        {
            // A session with no stored hash cannot be proven to belong to anyone, so nobody may resume it.
            return false;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return false;
        }

        if (!ReadTokens(scope, httpContext).TryGetValue(sessionId, out var token) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(token)),
            Encoding.UTF8.GetBytes(storedHash));
    }

    /// <summary>
    /// Removes the token for a session, so a completed or abandoned session can no longer be resumed from
    /// this browser.
    /// </summary>
    /// <param name="scope">The cookie scope.</param>
    /// <param name="sessionId">The session to forget.</param>
    public void Revoke(GuestSessionTokenScope scope, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var tokens = ReadTokens(scope, httpContext);

        if (tokens.Remove(sessionId))
        {
            WriteTokens(scope, httpContext, tokens);
        }
    }

    /// <summary>
    /// Returns the SHA-256 hash of a token, in the form stored on a session.
    /// </summary>
    /// <param name="token">The raw token.</param>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private Dictionary<string, string> ReadTokens(GuestSessionTokenScope scope, HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(scope.CookieName, out var value) || string.IsNullOrEmpty(value))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = _dataProtectionProvider.CreateProtector(scope.ProtectorPurpose).Unprotect(value);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // A tampered, truncated, or key-rotated cookie proves nothing. Treating it as empty means the
            // visitor simply cannot resume, which is the safe outcome.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void WriteTokens(GuestSessionTokenScope scope, HttpContext httpContext, Dictionary<string, string> tokens)
    {
        // Only the most recent sessions are worth carrying. Without a cap, a visitor who starts many
        // checkouts would grow the cookie until the browser silently dropped it and nobody could resume.
        if (tokens.Count > scope.MaxSessions)
        {
            foreach (var key in tokens.Keys.Take(tokens.Count - scope.MaxSessions).ToArray())
            {
                tokens.Remove(key);
            }
        }

        var protectedValue = _dataProtectionProvider.CreateProtector(scope.ProtectorPurpose).Protect(JsonSerializer.Serialize(tokens));

        httpContext.Response.Cookies.Append(scope.CookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.Add(scope.Lifetime),
        });
    }
}

/// <summary>
/// Identifies the cookie one feature's guest ownership tokens live in.
/// </summary>
/// <param name="CookieName">The cookie name.</param>
/// <param name="ProtectorPurpose">The data-protection purpose, which must be unique per feature so one
/// feature's cookie cannot be replayed as another's.</param>
/// <param name="Lifetime">How long the cookie lives.</param>
/// <param name="MaxSessions">How many sessions the cookie remembers.</param>
public sealed record GuestSessionTokenScope(
    string CookieName,
    string ProtectorPurpose,
    TimeSpan Lifetime,
    int MaxSessions = 10);
