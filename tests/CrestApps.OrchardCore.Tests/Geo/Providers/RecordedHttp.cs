using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Geo.Providers;

/// <summary>
/// Answers HTTP requests with recorded responses chosen by the request path, and keeps the requests.
/// </summary>
internal sealed class RecordedHttp : HttpMessageHandler
{
    private readonly List<(string PathPart, HttpStatusCode Status, string File)> _routes = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public RecordedHttp Answer(string pathPart, string file, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((pathPart, status, file));

        return this;
    }

    public HttpClient Client(string baseAddress = null)
        => new(this) { BaseAddress = baseAddress is null ? null : new Uri(baseAddress) };

    public static string Read(string file, [CallerFilePath] string path = null)
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "Recorded", file));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        var route = _routes.LastOrDefault(item => request.RequestUri!.ToString().Contains(item.PathPart, StringComparison.Ordinal));

        if (route.File is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }

        return new HttpResponseMessage(route.Status) { Content = new StringContent(Read(route.File), Encoding.UTF8, "application/json") };
    }
}
