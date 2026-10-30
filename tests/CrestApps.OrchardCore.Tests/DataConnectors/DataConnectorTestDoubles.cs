using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

internal sealed class FakeSecrets : IDataConnectorSecrets
{
    private readonly Dictionary<string, string> _values;

    public FakeSecrets(Dictionary<string, string> values = null)
    {
        _values = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string Get(string name)
        => _values.GetValueOrDefault(name);
}

/// <summary>
/// Answers HTTP requests from a function and records every request it was sent, with its body read.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _respond;

    public StubHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public List<(HttpMethod Method, Uri Uri, string Authorization, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (Requests)
        {
            Requests.Add((request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), body));
        }

        var response = _respond(request, body);
        response.RequestMessage ??= request;

        return response;
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
}

internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    public ITimeZone[] GetTimeZones() => [];

    public ITimeZone GetTimeZone(string timeZoneId) => null;

    public ITimeZone GetSystemTimeZone() => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
}

/// <summary>
/// A lock that holds keys in memory, so a test can hold a data set's lock while a refresh tries to take it.
/// </summary>
internal sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        lock (_held)
        {
            _held.Add(key);
        }

        return Task.FromResult<ILocker>(new Locker(this, key));
    }

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        lock (_held)
        {
            return _held.Add(key)
                ? Task.FromResult<(ILocker, bool)>((new Locker(this, key), true))
                : Task.FromResult<(ILocker, bool)>((null, false));
        }
    }

    public Task<bool> IsLockAcquiredAsync(string key)
    {
        lock (_held)
        {
            return Task.FromResult(_held.Contains(key));
        }
    }

    private void Release(string key)
    {
        lock (_held)
        {
            _held.Remove(key);
        }
    }

    private sealed class Locker : ILocker
    {
        private readonly InMemoryDistributedLock _owner;
        private readonly string _key;

        public Locker(InMemoryDistributedLock owner, string key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose() => _owner.Release(_key);

        public ValueTask DisposeAsync()
        {
            _owner.Release(_key);

            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Grants a principal the permissions named in its claims, and the permissions those imply, as Orchard Core's role
/// handler does.
/// </summary>
internal sealed class PermissionClaimsAuthorizationService : IAuthorizationService
{
    public static ClaimsPrincipal UserWith(params string[] permissions)
        => new(new ClaimsIdentity(permissions.Select(permission => new Claim(Permission.ClaimType, permission)), "Test"));

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            if (requirement is not PermissionRequirement permissionRequirement || !Grants(user, permissionRequirement.Permission))
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }
        }

        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        => Task.FromResult(AuthorizationResult.Failed());

    private static bool Grants(ClaimsPrincipal user, Permission permission)
    {
        if (user is null || permission is null)
        {
            return false;
        }

        if (user.HasClaim(Permission.ClaimType, permission.Name))
        {
            return true;
        }

        return (permission.ImpliedBy ?? []).Any(implied => Grants(user, implied));
    }
}

/// <summary>
/// A connector a test configures: its fields, its rows, what it fails with, and what it was asked to read.
/// </summary>
internal sealed class TestDataConnector : IDataConnector
{
    public static string Name => "Test";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TestDataConnector>("Test connector");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TestDataConnector>("Rows a test sets.");

    public static DataConnectorCapabilities Capabilities =>
        DataConnectorCapabilities.Live |
        DataConnectorCapabilities.Import |
        DataConnectorCapabilities.IncrementalImport |
        DataConnectorCapabilities.Discovery |
        DataConnectorCapabilities.ConditionPushdown;

    public List<DataField> Fields { get; set; } =
    [
        new("id", "id", DataFieldType.Integer) { IsIdentifier = true },
        new("name", "name", DataFieldType.Text),
        new("modified", "modified", DataFieldType.DateTime),
    ];

    public List<object[]> Rows { get; set; } = [];

    public Exception FailAfterFirstBatch { get; set; }

    public List<DataConnectorReadContext> Reads { get; } = [];

    public Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(DataConnectorTestResult.Success("ok"));

    public Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataConnectorResource>>([new("items", "Items", "Table")]);

    public Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataField>>([.. Fields]);

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Reads.Add(context);

        var watermarkIndex = context.WatermarkField is null ? -1 : Fields.FindIndex(field => field.Name == context.WatermarkField);
        var rows = Rows
            .Where(row => watermarkIndex < 0 || context.Watermark is null || DataValues.Compare(row[watermarkIndex], context.Watermark) > 0)
            .ToList();

        var size = Math.Max(1, context.BatchSize);
        var first = true;

        for (var start = 0; start < rows.Count || first; start += size)
        {
            if (!first && FailAfterFirstBatch is not null)
            {
                throw FailAfterFirstBatch;
            }

            first = false;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new DataBatch([.. Fields], [.. rows.Skip(start).Take(size)]);
        }
    }
}

internal static class DataConnectorTestData
{
    public static DataConnection Connection(string connector = "Test")
        => new()
        {
            ItemId = "connection1",
            Name = "Source",
            DisplayText = "Source",
            Connector = connector,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    public static ConnectedDataSet DataSet(DataSetAccessMode mode = DataSetAccessMode.Imported, string name = "Items")
        => new()
        {
            ItemId = "dataset1",
            Name = name,
            DisplayText = name,
            ConnectionId = "connection1",
            Resource = "items",
            AccessMode = mode,
            Fields =
            [
                new() { Name = "Id", SourceName = "id", DisplayName = "Id", Type = DataFieldType.Integer, SourceType = DataFieldType.Integer, IsIdentifier = true, IsIncluded = true },
                new() { Name = "Name", SourceName = "name", DisplayName = "Name", Type = DataFieldType.Text, SourceType = DataFieldType.Text, IsIncluded = true },
                new() { Name = "Modified", SourceName = "modified", DisplayName = "Modified", Type = DataFieldType.DateTime, SourceType = DataFieldType.DateTime, IsIncluded = true },
            ],
        };

    public static object[] Row(long id, string name, int day)
        => [id, name, new DateTime(2026, 10, day, 8, 0, 0, DateTimeKind.Utc)];
}
