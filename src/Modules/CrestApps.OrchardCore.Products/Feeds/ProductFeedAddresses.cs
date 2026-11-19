using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// The product feed's addresses and access key, shared by Settings › Commerce › Product feeds and the feed AI tools.
/// </summary>
internal static class ProductFeedAddresses
{
    /// <summary>
    /// The path of the XML feed.
    /// </summary>
    public const string XmlPath = "/feeds/products.xml";

    /// <summary>
    /// The path of the CSV feed.
    /// </summary>
    public const string CsvPath = "/feeds/products.csv";

    /// <summary>
    /// Makes a new access key for the feed addresses.
    /// </summary>
    /// <returns>The key, 32 hexadecimal characters.</returns>
    public static string NewKey()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>
    /// Gets the address the feeds are under: the site's base URL, else the request's.
    /// </summary>
    /// <param name="site">The site.</param>
    /// <param name="request">The current request, or <see langword="null"/>.</param>
    /// <returns>The base address, without a trailing slash, or <see langword="null"/>.</returns>
    public static string GetBaseUrl(ISite site, HttpRequest request)
        => site?.BaseUrl?.TrimEnd('/') ?? (request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}");

    /// <summary>
    /// Gets a feed's address.
    /// </summary>
    /// <param name="baseUrl">The base address.</param>
    /// <param name="path">The feed's path.</param>
    /// <param name="key">The access key, or <see langword="null"/> when the feed is public.</param>
    /// <returns>The address.</returns>
    public static string Build(string baseUrl, string path, string key)
        => $"{baseUrl}{path}{(string.IsNullOrEmpty(key) ? string.Empty : "?key=" + Uri.EscapeDataString(key))}";
}
