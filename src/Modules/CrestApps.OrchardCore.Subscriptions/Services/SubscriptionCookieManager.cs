using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Manages the cookie that maps subscription content items to subscription session identifiers.
/// </summary>
public sealed class SubscriptionCookieManager
{
    private const string _cookieName = "subscriptions";

    private readonly HttpContext _httpContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionCookieManager"/> class.
    /// </summary>
    /// <param name="httpContext">The HTTP context used to read and write the subscription cookie.</param>
    public SubscriptionCookieManager(HttpContext httpContext)
    {
        _httpContext = httpContext;
    }

    /// <summary>
    /// Adds or updates the stored session identifier for a subscription content item.
    /// </summary>
    /// <param name="subscriptionContentItemId">The subscription content item identifier.</param>
    /// <param name="sessionId">The subscription session identifier to store.</param>
    public void Append(string subscriptionContentItemId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionContentItemId);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var values = GetValues();

        values[subscriptionContentItemId] = sessionId;

        SetValue(values);
    }

    /// <summary>
    /// Attempts to get the stored session identifier for a subscription content item.
    /// </summary>
    /// <param name="subscriptionContentItemId">The subscription content item identifier.</param>
    /// <param name="sessionId">When this method returns, contains the stored subscription session identifier when found.</param>
    /// <returns><see langword="true"/> when a stored session identifier exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string subscriptionContentItemId, out string sessionId)
    {
        if (subscriptionContentItemId != null)
        {
            return GetValues().TryGetValue(subscriptionContentItemId, out sessionId);
        }

        sessionId = null;

        return false;
    }

    /// <summary>
    /// Removes the stored session identifier for a subscription content item.
    /// </summary>
    /// <param name="subscriptionContentItemId">The subscription content item identifier to remove.</param>
    public void Remove(string subscriptionContentItemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionContentItemId);

        var values = GetValues();

        values.Remove(subscriptionContentItemId);

        SetValue(values);
    }

    /// <summary>
    /// Removes the subscription cookie.
    /// </summary>
    public void Remove()
    {
        _httpContext.Response.Cookies.Delete(_cookieName);
    }

    private void SetValue(Dictionary<string, string> values)
    {
        _httpContext.Response.Cookies.Delete(_cookieName);

        if (values.Count > 0)
        {
            _httpContext.Response.Cookies.Append(_cookieName, JsonSerializer.Serialize(values), new CookieOptions()
            {
                HttpOnly = true,
                Secure = true
            });
        }
    }

    /// <summary>
    /// Gets all stored subscription content item and session identifier mappings from the cookie.
    /// </summary>
    /// <returns>The stored subscription content item and session identifier mappings.</returns>
    public Dictionary<string, string> GetValues()
    {
        if (_httpContext.Request.Cookies.TryGetValue(_cookieName, out var value))
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(value);
            }
            catch { }
        }

        return [];
    }
}
