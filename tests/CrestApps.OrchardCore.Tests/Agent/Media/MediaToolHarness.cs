using System.Net;
using System.Text;
using CrestApps.OrchardCore.AI.Agent.Media;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.Media;

/// <summary>
/// The services of a media tool call: an in-memory media store, media options with allowed and restricted extensions,
/// the file creation pipeline of an upload, and a downloader whose requests and host addresses the test supplies.
/// </summary>
internal sealed class MediaToolHarness
{
    /// <summary>
    /// A public address a test host resolves to.
    /// </summary>
    public static readonly IPAddress PublicAddress = IPAddress.Parse("203.0.113.10");

    private readonly List<string> _tempFiles = [];

    /// <summary>
    /// Gets the media store.
    /// </summary>
    public InMemoryMediaFileStore Store { get; } = new();

    /// <summary>
    /// Gets the media options: .png, .jpg, .pdf, .csv and .txt are allowed, .svg, .css and .js are restricted, and a
    /// file may have 1000 bytes.
    /// </summary>
    public MediaOptions MediaOptions { get; } = new()
    {
        AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".pdf", ".csv", ".txt" },
        RestrictedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".svg", ".css", ".js" },
        MaxFileSize = 1000,
        MaxUploadChunkSize = 500,
        SupportedSizes = [16, 32, 64],
        UseTokenizedQueryString = true,
        CdnBaseUrl = "https://cdn.example.com",
        AssetsRequestPath = "/media",
        AssetsPath = "Media",
        AssetsUsersFolder = "_Users",
        MaxBrowserCacheDays = 30,
        MaxCacheDays = 365,
        TemporaryFileLifetime = TimeSpan.FromHours(1),
    };

    /// <summary>
    /// Gets the handlers of the file creation pipeline.
    /// </summary>
    public List<IFileEventHandler> FileEventHandlers { get; } = [];

    /// <summary>
    /// Gets the requests the downloader sent, in order.
    /// </summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>
    /// Gets or sets whether a user is signed in.
    /// </summary>
    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants or refuses a permission on a resource. The default grants everything.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Gets or sets what a remote address answers. The default answers every request with a small PNG.
    /// </summary>
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => CreateResponse("png-bytes", "image/png");

    /// <summary>
    /// Gets or sets the addresses a host name resolves to. The default resolves every host to a public address.
    /// </summary>
    public Func<string, IPAddress[]> ResolveHost { get; set; } = _ => [PublicAddress];

    /// <summary>
    /// Gets or sets the IDs of the enabled features.
    /// </summary>
    public string[] EnabledFeatures { get; set; } = ["OrchardCore.Media", "OrchardCore.Media.Cache", "OrchardCore.Contents"];

    /// <summary>
    /// Gets or sets a step that adds or replaces services before the provider is built.
    /// </summary>
    public Action<IServiceCollection> Configure { get; set; }

    /// <summary>
    /// Gets the temporary files the downloader made.
    /// </summary>
    public IReadOnlyList<string> TempFiles => _tempFiles;

    /// <summary>
    /// Refuses the folder permission on the paths that start with a prefix and grants everything else.
    /// </summary>
    /// <param name="prefix">The start of the refused paths.</param>
    public void RefuseFolder(string prefix)
    {
        Authorize = (permission, resource) =>
            permission.Name != MediaPermissions.ManageMediaFolder.Name ||
            resource is not string path ||
            !path.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Refuses one permission and grants everything else.
    /// </summary>
    /// <param name="refused">The refused permission.</param>
    public void Refuse(Permission refused)
        => Authorize = (permission, _) => permission.Name != refused.Name;

    /// <summary>
    /// Creates the downloader the import tool uses, with the requests and the host addresses of the harness.
    /// </summary>
    /// <returns>The downloader.</returns>
    public RemoteMediaDownloader CreateDownloader()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();

        httpClientFactory
            .Setup(factory => factory.CreateClient(RemoteMediaDownloader.HttpClientName))
            .Returns(() => new HttpClient(new StubHandler(this)));

        var tempDirectoryProvider = new Mock<ITempDirectoryProvider>();

        tempDirectoryProvider
            .Setup(provider => provider.GetTempFileName(It.IsAny<string>()))
            .Returns(() =>
            {
                var path = Path.Combine(Path.GetTempPath(), $"media-tool-test-{Guid.NewGuid():N}");

                _tempFiles.Add(path);

                return path;
            });

        return new RemoteMediaDownloader(
            httpClientFactory.Object,
            tempDirectoryProvider.Object,
            (host, _) => Task.FromResult(ResolveHost(host)));
    }

    /// <summary>
    /// Builds the services of the tool call.
    /// </summary>
    /// <returns>The service provider.</returns>
    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, (permission, resource) => Authorize(permission, resource));

        var shellFeaturesManager = new Mock<IShellFeaturesManager>();

        shellFeaturesManager
            .Setup(manager => manager.GetEnabledFeaturesAsync())
            .ReturnsAsync(() => EnabledFeatures.Select(CreateFeature).ToList());

        services.AddSingleton<IMediaFileStore>(Store);
        services.AddSingleton(Options.Create(MediaOptions));
        services.AddSingleton<IContentTypeProvider>(new FileExtensionContentTypeProvider());
        services.AddSingleton(new FileCreationService(FileEventHandlers));
        services.AddSingleton(shellFeaturesManager.Object);
        services.AddSingleton(CreateDownloader());

        Configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates the answer of a remote address.
    /// </summary>
    /// <param name="content">The body.</param>
    /// <param name="contentType">The media type of the body.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage CreateResponse(string content, string contentType)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType),
        };
    }

    /// <summary>
    /// Creates a redirect to another address.
    /// </summary>
    /// <param name="location">The address to go to.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage CreateRedirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);

        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);

        return response;
    }

    private static IFeatureInfo CreateFeature(string id)
    {
        var feature = new Mock<IFeatureInfo>();

        feature.Setup(info => info.Id).Returns(id);

        return feature.Object;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly MediaToolHarness _harness;

        public StubHandler(MediaToolHarness harness)
        {
            _harness = harness;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _harness.Requests.Add(request.RequestUri);

            return Task.FromResult(_harness.Respond(request));
        }
    }
}
