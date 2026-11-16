using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

/// <summary>
/// The LRS core on a real store, with an in-memory attachment store and a clock the test moves.
/// </summary>
internal sealed class XapiTestHarness : IAsyncDisposable
{
    private XapiTestHarness(CalendarTestHarness inner, InMemoryProtectedFiles<XapiAttachmentFiles> files, TestClock clock)
    {
        Inner = inner;
        Files = files;
        Clock = clock;
    }

    public CalendarTestHarness Inner { get; }

    public InMemoryProtectedFiles<XapiAttachmentFiles> Files { get; }

    public TestClock Clock { get; }

    /// <summary>
    /// A client that may write and read everything and manage documents.
    /// </summary>
    public static XapiCredential Admin { get; } = Client("lrs-admin", write: true, readAll: true, documents: true);

    public static XapiCredential Client(string clientId, bool write = false, bool readAll = false, bool readOwn = false, bool documents = false)
        => new()
        {
            ClientId = clientId,
            Name = clientId,
            Authority = new JsonObject
            {
                ["objectType"] = "Agent",
                ["name"] = clientId,
                ["account"] = new JsonObject { ["homePage"] = "https://lrs.example.com", ["name"] = clientId },
            },
            AuthorityKey = "client:" + clientId,
            CanWriteStatements = write,
            CanReadAllStatements = readAll,
            CanReadOwnStatements = readOwn,
            CanManageDocuments = documents,
        };

    public static async Task<XapiTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var files = new InMemoryProtectedFiles<XapiAttachmentFiles>();
        var clock = new TestClock { UtcNow = LearningTestHarness.Now };
        var protection = new EphemeralDataProtectionProvider();

        var inner = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddHttpContextAccessor();
                services.AddLearningXapiCore();
                services.RemoveAll<IProtectedFileStore<XapiAttachmentFiles>>();
                services.AddSingleton<IProtectedFileStore<XapiAttachmentFiles>>(files);
                services.RemoveAll<IDataProtectionProvider>();
                services.AddSingleton<IDataProtectionProvider>(protection);
                configure?.Invoke(services);
            },
            XapiIndexSchema.CreateAllAsync,
            [.. XapiIndexSchema.IndexProviders()],
            [XapiConstants.CollectionName]);

        return new XapiTestHarness(inner, files, clock);
    }

    public Task<XapiResponse> StatementsAsync(XapiRequest request, XapiCredential credential = null)
        => Inner.RunAsync(services => services.GetRequiredService<XapiStatementsResource>().HandleAsync(request, credential ?? Admin), commit: false);

    public Task<XapiResponse> HandleAsync(XapiRequest request, XapiCredential credential)
        => Inner.RunAsync(services => services.GetRequiredService<XapiEndpointHandler>().HandleAsync(request, credential), commit: false);

    public Task<XapiResponse> DocumentAsync(string method, string resource, IDictionary<string, string> query, object body = null, XapiCredential credential = null, IDictionary<string, string> headers = null, string contentType = null)
    {
        var request = XapiRequest.Create(method, resource, body, query);

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers[name] = value;
        }

        if (contentType is not null)
        {
            request.Headers["Content-Type"] = contentType;
        }

        return HandleAsync(request, credential ?? Admin);
    }

    public Task<XapiResponse> PutAsync(string id, object statement, XapiCredential credential = null)
        => StatementsAsync(XapiRequest.Create("PUT", "statements", statement, new Dictionary<string, string> { ["statementId"] = id }), credential);

    public Task<XapiResponse> PostAsync(object statements, XapiCredential credential = null)
        => StatementsAsync(XapiRequest.Create("POST", "statements", statements), credential);

    public Task<XapiResponse> GetAsync(IDictionary<string, string> query, XapiCredential credential = null, string acceptLanguage = null)
    {
        var request = XapiRequest.Create("GET", "statements", query: query);

        if (acceptLanguage is not null)
        {
            request.Headers["Accept-Language"] = acceptLanguage;
        }

        return StatementsAsync(request, credential);
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Inner.RunAsync(work);

    public ValueTask DisposeAsync()
        => Inner.DisposeAsync();
}

/// <summary>
/// A protected file area in memory.
/// </summary>
internal sealed class InMemoryProtectedFiles<TArea> : IProtectedFileStore<TArea>
{
    private int _next;

    public ConcurrentDictionary<string, (ProtectedFile File, byte[] Content)> Stored { get; } = new(StringComparer.Ordinal);

    public List<string> Deleted { get; } = [];

    public async Task<ProtectedFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var id = "file" + Interlocked.Increment(ref _next);
        var file = new ProtectedFile(id, fileName, buffer.Length);

        Stored[id] = (file, buffer.ToArray());

        return file;
    }

    public Task<ProtectedFile> FindAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult(fileId is not null && Stored.TryGetValue(fileId, out var stored) ? stored.File : null);

    public Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(fileId is not null && Stored.TryGetValue(fileId, out var stored) ? new MemoryStream(stored.Content, writable: false) : null);

    public Task<bool> DeleteAsync(string fileId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(fileId);

        return Task.FromResult(Stored.TryRemove(fileId, out _));
    }
}
