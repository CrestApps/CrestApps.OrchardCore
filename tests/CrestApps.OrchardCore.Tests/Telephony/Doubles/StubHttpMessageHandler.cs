using System.Net;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records requests and returns a configured response.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody = "")
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    public HttpRequestMessage LastRequest { get; private set; }

    public string LastRequestBody { get; private set; }

    public IList<HttpRequestMessage> Requests { get; } = [];

    public IList<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        Requests.Add(request);

        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(LastRequestBody);
        }
        else
        {
            RequestBodies.Add(null);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody),
        };
    }
}
