using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SubscriptionCookieManager
{
    private const string _cookieName = "subscriptions";

    private readonly HttpContext _httpContext;

    public SubscriptionCookieManager(HttpContext httpContext)
    {
        _httpContext = httpContext;
    }

    public void Append(string subscriptionContentItemId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionContentItemId);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var values = GetValues();

        values[subscriptionContentItemId] = sessionId;

        SetValue(values);
    }

    public bool TryGetValue(string subscriptionContentItemId, out string sessionId)
    {
        if (subscriptionContentItemId != null)
        {
            return GetValues().TryGetValue(subscriptionContentItemId, out sessionId);
        }

        sessionId = null;

        return false;
    }

    public void Remove(string subscriptionContentItemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionContentItemId);

        var values = GetValues();

        values.Remove(subscriptionContentItemId);

        SetValue(values);
    }

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
                HttpOnly = true
            });
        }
    }

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
