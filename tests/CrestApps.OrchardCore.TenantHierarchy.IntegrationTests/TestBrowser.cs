using System.Net;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// A small browser for tests. It sends every request to the test host on the loopback address with the tenant's host
/// name in the Host header, keeps host-only cookies per host like a browser does, and follows redirects one at a time
/// so a test can see every hop.
/// </summary>
public sealed partial class TestBrowser : IDisposable
{
    private readonly HttpClient _client;
    private readonly int _port;
    private readonly Dictionary<string, Dictionary<string, string>> _cookies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBrowser"/> class.
    /// </summary>
    /// <param name="port">The port of the test host.</param>
    public TestBrowser(int port)
    {
        _port = port;
        _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        });
    }

    /// <summary>
    /// Gets the hops of the last <see cref="NavigateAsync"/> call.
    /// </summary>
    public List<Uri> LastHops { get; } = [];

    /// <summary>
    /// Sends a GET request without following redirects.
    /// </summary>
    /// <param name="url">The absolute URL, with the tenant host.</param>
    /// <param name="headers">More request headers.</param>
    public Task<HttpResponseMessage> GetAsync(string url, IDictionary<string, string> headers = null)
        => SendAsync(HttpMethod.Get, new Uri(url), null, headers);

    /// <summary>
    /// Sends a POST request with form fields without following redirects.
    /// </summary>
    /// <param name="url">The absolute URL, with the tenant host.</param>
    /// <param name="fields">The form fields.</param>
    public Task<HttpResponseMessage> PostAsync(string url, IDictionary<string, string> fields)
        => SendAsync(HttpMethod.Post, new Uri(url), new FormUrlEncodedContent(fields), null);

    /// <summary>
    /// Sends a GET request and follows redirects, across hosts, until a response that is not a redirect.
    /// </summary>
    /// <param name="url">The absolute URL, with the tenant host.</param>
    /// <param name="maxHops">The highest number of redirects to follow.</param>
    public async Task<HttpResponseMessage> NavigateAsync(string url, int maxHops = 10)
    {
        LastHops.Clear();

        var current = new Uri(url);

        for (var hop = 0; hop <= maxHops; hop++)
        {
            LastHops.Add(current);

            var response = await SendAsync(HttpMethod.Get, current, null, null);

            if ((int)response.StatusCode is < 300 or >= 400 || response.Headers.Location is null)
            {
                return response;
            }

            current = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(current, response.Headers.Location);
        }

        throw new InvalidOperationException("Too many redirects.");
    }

    /// <summary>
    /// Signs in to a tenant with the login form.
    /// </summary>
    /// <param name="baseAddress">The base address of the tenant, for example <c>http://firma.localhost</c>.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <returns>The response to the login post.</returns>
    public async Task<HttpResponseMessage> SignInAsync(string baseAddress, string userName, string password)
    {
        var token = await GetAntiforgeryTokenAsync($"{baseAddress}/Login");

        return await PostAsync($"{baseAddress}/Login", new Dictionary<string, string>
        {
            ["LoginForm.UserName"] = userName,
            ["LoginForm.Password"] = password,
            ["__RequestVerificationToken"] = token,
        });
    }

    /// <summary>
    /// Loads a page and returns the antiforgery token of its first form.
    /// </summary>
    /// <param name="url">The absolute URL of the page.</param>
    public async Task<string> GetAntiforgeryTokenAsync(string url)
    {
        var response = await NavigateAsync(url);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = AntiforgeryTokenRegex().Match(html);

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups["token"].Value)
            : throw new InvalidOperationException($"No antiforgery token on {url} ({(int)response.StatusCode}).");
    }

    /// <summary>
    /// Returns whether the browser holds a cookie whose name starts with the given text for a host.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="namePrefix">The start of the cookie name.</param>
    public bool HasCookie(string host, string namePrefix)
        => _cookies.TryGetValue(host, out var jar) && jar.Keys.Any(name => name.StartsWith(namePrefix, StringComparison.Ordinal));

    /// <summary>
    /// Removes every cookie of a host.
    /// </summary>
    /// <param name="host">The host.</param>
    public void ClearCookies(string host)
        => _cookies.Remove(host);

    /// <inheritdoc/>
    public void Dispose()
        => _client.Dispose();

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri url, HttpContent content, IDictionary<string, string> headers)
    {
        var target = new UriBuilder(url)
        {
            Host = "127.0.0.1",
            Port = _port,
        }.Uri;

        using var request = new HttpRequestMessage(method, target)
        {
            Content = content,
        };

        request.Headers.Host = $"{url.Host}:{_port}";

        if (_cookies.TryGetValue(url.Host, out var jar) && jar.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", jar.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        foreach (var header in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await _client.SendAsync(request);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            StoreCookies(url.Host, setCookies);
        }

        // A redirect to a relative location stays on the tenant host, not on the loopback address the request used.
        if (response.Headers.Location is { IsAbsoluteUri: true } location && location.Host == "127.0.0.1")
        {
            response.Headers.Location = new UriBuilder(location) { Host = url.Host, Port = url.Port }.Uri;
        }

        return response;
    }

    private void StoreCookies(string host, IEnumerable<string> setCookies)
    {
        if (!_cookies.TryGetValue(host, out var jar))
        {
            jar = new Dictionary<string, string>(StringComparer.Ordinal);
            _cookies[host] = jar;
        }

        foreach (var setCookie in setCookies)
        {
            var parts = setCookie.Split(';', StringSplitOptions.TrimEntries);
            var pair = parts[0].Split('=', 2);
            var name = pair[0];
            var value = pair.Length > 1 ? pair[1] : string.Empty;
            var expired = parts.Any(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.TryParse(part.AsSpan("expires=".Length), out var expires) &&
                expires < DateTimeOffset.UtcNow);

            if (expired || string.IsNullOrEmpty(value))
            {
                jar.Remove(name);
            }
            else
            {
                jar[name] = value;
            }
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
