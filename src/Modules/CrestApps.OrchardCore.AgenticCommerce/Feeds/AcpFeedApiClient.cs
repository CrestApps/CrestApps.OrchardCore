using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Calls an agent platform's Agentic Commerce Protocol feed API (ACP 2026-04-17): creates the store's feed and upserts
/// its products, with the platform's bearer key, the API version and an idempotency key on every call. Calls go through
/// the egress-guarded HTTP client, so a feed address can't reach the site's own network.
/// </summary>
public sealed class AcpFeedApiClient
{
    /// <summary>
    /// The name of the HTTP client the calls go through.
    /// </summary>
    public const string HttpClientName = "CrestApps.OrchardCore.AgenticCommerce.Feeds.Acp";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpFeedApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public AcpFeedApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Creates a feed (<c>POST /feeds</c>) and returns its id.
    /// </summary>
    /// <param name="baseUrl">The base address of the feed API.</param>
    /// <param name="bearerToken">The platform's bearer key.</param>
    /// <param name="targetCountry">The two-letter code of the country the feed is for, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The idempotency key, the same on each retry.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    public async Task<string> CreateFeedAsync(string baseUrl, string bearerToken, string targetCountry, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject();

        if (!string.IsNullOrWhiteSpace(targetCountry))
        {
            body["target_country"] = targetCountry.Trim().ToUpperInvariant();
        }

        using var request = CreateRequest(HttpMethod.Post, Combine(baseUrl, "feeds"), bearerToken, idempotencyKey, body);

        var response = await SendAsync(request, cancellationToken);
        var id = ReadString(response, "id");

        return string.IsNullOrEmpty(id)
            ? throw new HttpRequestException("The feed API created a feed without an id.")
            : id;
    }

    /// <summary>
    /// Upserts products into a feed (<c>PATCH /feeds/{id}/products</c>), and returns the API's answer.
    /// </summary>
    /// <param name="baseUrl">The base address of the feed API.</param>
    /// <param name="bearerToken">The platform's bearer key.</param>
    /// <param name="feedId">The feed's id.</param>
    /// <param name="products">The ACP products, complete.</param>
    /// <param name="idempotencyKey">The idempotency key, the same on each retry.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    public async Task<JsonNode> UpsertProductsAsync(string baseUrl, string bearerToken, string feedId, IReadOnlyList<JsonObject> products, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(feedId);
        ArgumentNullException.ThrowIfNull(products);

        using var request = CreateRequest(HttpMethod.Patch, Combine(baseUrl, $"feeds/{Uri.EscapeDataString(feedId)}/products"), bearerToken, idempotencyKey, CreateUpsertBody(products));

        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Returns the body of an upsert: <c>{"products": [...]}</c>.
    /// </summary>
    /// <param name="products">The ACP products.</param>
    public static JsonObject CreateUpsertBody(IEnumerable<JsonObject> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        return new JsonObject
        {
            ["products"] = new JsonArray([.. products.Select(product => product.DeepClone())]),
        };
    }

    /// <summary>
    /// Returns a request to the feed API with ACP's headers: <c>Authorization: Bearer</c>, <c>API-Version</c> and
    /// <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The address.</param>
    /// <param name="bearerToken">The platform's bearer key.</param>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <param name="body">The JSON body.</param>
    public static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string bearerToken, string idempotencyKey, JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrEmpty(bearerToken);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var request = new HttpRequestMessage(method, uri)
        {
            Content = new StringContent(body?.ToJsonString() ?? "{}", Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("API-Version", AgenticCommerceConstants.Versions.Acp20260417);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        return request;
    }

    /// <summary>
    /// Returns an idempotency key made from stable values, such as the target, the feed and the body sent, so a retry
    /// of the same call carries the same key.
    /// </summary>
    /// <param name="parts">The stable values.</param>
    public static string CreateIdempotencyKey(params IEnumerable<string> parts)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts ?? [])))).ToLowerInvariant();

    /// <summary>
    /// Returns the address of a path under the feed API's base address.
    /// </summary>
    /// <param name="baseUrl">The base address of the feed API.</param>
    /// <param name="path">The path, such as <c>feeds</c>.</param>
    public static Uri Combine(string baseUrl, string path)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var root))
        {
            throw new ArgumentException("The feed API's base address isn't an absolute address.", nameof(baseUrl));
        }

        var text = root.GetLeftPart(UriPartial.Path).TrimEnd('/');

        return new Uri(text + "/" + path.TrimStart('/'));
    }

    private async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // ACP's errors are flat: a code and a message.
            var code = ReadString(Parse(text), "code");

            throw new HttpRequestException($"The feed API answered {(int)response.StatusCode}{(string.IsNullOrEmpty(code) ? string.Empty : " (" + code + ")")}.", null, response.StatusCode);
        }

        return Parse(text);
    }

    private static JsonNode Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadString(JsonNode node, string name)
        => node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
