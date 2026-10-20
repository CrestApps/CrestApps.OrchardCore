using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Companies.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// OCI punchout: a company's procurement system opens the store with the company's punchout key and the address to send
/// the cart back to (its HOOK_URL). The store signs in the company's punchout buyer; the buyer fills a cart and
/// transfers it back as OCI fields, and the order is placed in the procurement system.
/// </summary>
public sealed class PunchoutService
{
    public const string CookieName = "punchout";

    // A key is 32 random bytes, as base64url.
    private const int KeyBytes = 32;

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PunchoutService(IDataProtectionProvider dataProtectionProvider, IHttpContextAccessor httpContextAccessor)
    {
        _protector = dataProtectionProvider.CreateProtector("CrestApps.Companies.Punchout");
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Makes a new punchout key: returns it, to show once, and the hash the company keeps.
    /// </summary>
    public static (string Key, string Hash) NewKey()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (key, Hash(key));
    }

    public static string Hash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key ?? string.Empty)));

    /// <summary>
    /// Returns whether a key is the company's punchout key.
    /// </summary>
    public static bool IsKey(Company company, string key)
        => company?.Punchout is { Enabled: true, KeyHash.Length: > 0 } punchout &&
            !string.IsNullOrEmpty(key) &&
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(key)), Encoding.ASCII.GetBytes(punchout.KeyHash));

    /// <summary>
    /// Returns a HOOK_URL that can take the cart: an absolute https address, or http on the local machine.
    /// </summary>
    public static Uri ParseHookUrl(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) &&
            string.IsNullOrEmpty(uri.UserInfo)
                ? uri
                : null;

    /// <summary>
    /// Remembers the punchout for this browser: the company, and where its cart goes back to.
    /// </summary>
    public void Start(string companyId, Uri hookUrl, string target)
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("A punchout starts in a request.");
        var payload = JsonSerializer.Serialize(new PunchoutSession(companyId, hookUrl.AbsoluteUri, SafeTarget(target)));

        httpContext.Response.Cookies.Append(CookieName, _protector.Protect(payload), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Path = string.IsNullOrEmpty(httpContext.Request.PathBase) ? "/" : httpContext.Request.PathBase.Value,
        });
    }

    /// <summary>
    /// Returns this browser's punchout, or <see langword="null"/>.
    /// </summary>
    public PunchoutSession GetCurrent()
    {
        if (_httpContextAccessor.HttpContext?.Request.Cookies[CookieName] is not { Length: > 0 } cookie)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PunchoutSession>(_protector.Unprotect(cookie));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void End()
        => _httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = string.IsNullOrEmpty(_httpContextAccessor.HttpContext.Request.PathBase) ? "/" : _httpContextAccessor.HttpContext.Request.PathBase.Value,
        });

    /// <summary>
    /// Returns the OCI fields for a cart, in order: each line's description, quantity, unit, price, currency, SKU and
    /// product id, numbered from 1.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ToOciFields(CartPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var fields = new List<KeyValuePair<string, string>>();
        var number = 0;

        foreach (var line in pricing.Lines.Where(line => line.Problem is null && line.Line.Quantity > 0))
        {
            number++;

            var description = line.Title;

            // What one unit costs with its add-ons.
            var unitPrice = Math.Round(line.LineTotal / line.Line.Quantity, 4, MidpointRounding.AwayFromZero);

            Add(fields, "DESCRIPTION", number, Truncate(description, 40));
            Add(fields, "QUANTITY", number, line.Line.Quantity.ToString(CultureInfo.InvariantCulture));
            Add(fields, "UNIT", number, "EA");
            Add(fields, "PRICE", number, unitPrice.ToString("0.00##", CultureInfo.InvariantCulture));
            Add(fields, "PRICEUNIT", number, "1");
            Add(fields, "CURRENCY", number, pricing.Currency);
            Add(fields, "VENDORMAT", number, Truncate(line.Product?.Sku, 40));
            Add(fields, "EXT_PRODUCT_ID", number, line.Line.ContentItemId);

            if (description?.Length > 40)
            {
                fields.Add(new($"NEW_ITEM-LONGTEXT_{number}:132[]", description));
            }
        }

        return fields;
    }

    private static void Add(List<KeyValuePair<string, string>> fields, string name, int number, string value)
        => fields.Add(new($"NEW_ITEM-{name}[{number}]", value ?? string.Empty));

    private static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length];

    // OCI names the frame to send the cart back to: _top, _parent or _self, never a script.
    private static string SafeTarget(string target)
        => target is "_top" or "_parent" or "_self" or "_blank" ? target : "_top";
}

/// <summary>
/// A browser's punchout: the company it's for, and where the cart goes back to.
/// </summary>
public sealed record PunchoutSession(string CompanyId, string HookUrl, string Target);
