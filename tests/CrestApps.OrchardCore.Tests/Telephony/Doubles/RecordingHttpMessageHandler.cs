using System.Net;
using System.Net.Http.Headers;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A stand-in for a provider's HTTP API that records what was asked of it and answers with canned responses.
/// <para>
/// Provider code is the part of this platform that cannot be exercised for real in a test run, so the only way
/// to assert that it sends the right verb to the right path with the right body — and behaves correctly when the
/// provider answers 429, 500 or nothing at all — is to put a handler in front of it and read what came through.
/// </para>
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    private Func<HttpRequestMessage, HttpResponseMessage> _fallback;

    /// <summary>
    /// Every request that reached the handler, in order, with its body already read.
    /// </summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// Queues one response. Queued responses are used in order, so a test can describe a sequence such as "the
    /// first attempt is rate limited, the retry succeeds".
    /// </summary>
    /// <param name="statusCode">The status to answer with.</param>
    /// <param name="json">The response body.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> header to send, when the provider would send one.</param>
    public RecordingHttpMessageHandler RespondWith(HttpStatusCode statusCode, string json = "{}", TimeSpan? retryAfter = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json ?? string.Empty),
            };

            if (retryAfter is not null)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
            }

            return response;
        });

        return this;
    }

    /// <summary>
    /// Answers every request that outlives the queue this way, so a test that does not care how many calls are
    /// made does not have to count them.
    /// </summary>
    /// <param name="statusCode">The status to answer with.</param>
    /// <param name="json">The response body.</param>
    public RecordingHttpMessageHandler AlwaysRespondWith(HttpStatusCode statusCode, string json = "{}")
    {
        _fallback = _ => new HttpResponseMessage(statusCode) { Content = new StringContent(json ?? string.Empty) };

        return this;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            body,
            request.Headers.Authorization?.ToString()));

        if (_responses.Count > 0)
        {
            return _responses.Dequeue()(request);
        }

        if (_fallback is not null)
        {
            return _fallback(request);
        }

        // A test that did not describe a response for a call the code made has found a call it did not expect,
        // which is more useful reported than silently answered with a default.
        throw new InvalidOperationException(
            $"The provider double received an unexpected {request.Method} {request.RequestUri}. Queue a response for it, or use AlwaysRespondWith.");
    }

    /// <summary>
    /// One request the code under test sent.
    /// </summary>
    /// <param name="Method">The HTTP verb.</param>
    /// <param name="Uri">The absolute request URI.</param>
    /// <param name="Body">The request body, when there was one.</param>
    /// <param name="Authorization">The authorization header, so a test can assert the bearer was attached.</param>
    internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string Authorization)
    {
        /// <summary>
        /// The path and query, which is what a test usually wants to assert on.
        /// </summary>
        public string Path => Uri?.PathAndQuery ?? string.Empty;
    }
}
