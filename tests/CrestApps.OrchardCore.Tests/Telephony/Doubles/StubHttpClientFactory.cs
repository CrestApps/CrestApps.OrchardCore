namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// An <see cref="IHttpClientFactory"/> that returns clients backed by a fixed handler.
/// </summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false);
}
