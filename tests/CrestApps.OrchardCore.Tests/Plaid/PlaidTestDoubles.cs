using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Plaid;

/// <summary>
/// Answers Plaid calls by path, in order, and records every request with its body.
/// </summary>
internal sealed class FakePlaidHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<(HttpStatusCode Status, string Body)>> _answers = new(StringComparer.Ordinal);

    public List<(string Path, HttpRequestMessage Request, JsonObject Body)> Calls { get; } = [];

    public FakePlaidHandler Answer(string path, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        if (!_answers.TryGetValue(path, out var queue))
        {
            _answers[path] = queue = new Queue<(HttpStatusCode, string)>();
        }

        queue.Enqueue((status, body));

        return this;
    }

    public FakePlaidHandler Error(string path, string errorType, string errorCode, HttpStatusCode status = HttpStatusCode.BadRequest, string displayMessage = null)
        => Answer(path, new JsonObject
        {
            ["error_type"] = errorType,
            ["error_code"] = errorCode,
            ["error_message"] = "developer message",
            ["display_message"] = displayMessage,
            ["request_id"] = "req-1",
        }.ToJsonString(), status);

    public IEnumerable<(string Path, HttpRequestMessage Request, JsonObject Body)> CallsTo(string path)
        => Calls.Where(call => call.Path == path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;

        Calls.Add((path, request, body));

        if (!_answers.TryGetValue(path, out var queue) || queue.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }

        var (status, text) = queue.Count == 1 ? queue.Peek() : queue.Dequeue();

        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }
}

/// <summary>
/// Hands out clients over one handler.
/// </summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false);
}

/// <summary>
/// Fixed credentials.
/// </summary>
internal sealed class FixedPlaidCredentials : IPlaidCredentialsProvider
{
    public static readonly PlaidCredentials Sandbox = new(PlaidEnvironment.Sandbox, "client-1", "secret-1");

    public PlaidCredentials Credentials { get; set; } = Sandbox;

    public Task<PlaidCredentials> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Credentials);
}

/// <summary>
/// A fixed link token request.
/// </summary>
internal sealed class FixedPlaidLinkSite : IPlaidLinkSite
{
    public Task<PlaidLinkTokenRequest> CreateLinkTokenRequestAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new PlaidLinkTokenRequest
        {
            ClientUserId = "hashed-" + userId,
            ClientName = "Test Site",
            Language = "en",
            WebhookUrl = "https://example.com/plaid/webhook",
        });
}

/// <summary>
/// Kept items in memory.
/// </summary>
internal sealed class InMemoryPlaidItemStore : IPlaidItemStore
{
    public List<PlaidItem> Items { get; } = [];

    public Task<PlaidItem> FindByItemIdAsync(string itemId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(item => item.ItemId == itemId));

    public Task<IReadOnlyList<PlaidItem>> ListByItemIdAsync(string itemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PlaidItem>>(Items.Where(item => item.ItemId == itemId).ToList());

    public Task<IReadOnlyList<PlaidItem>> ListByBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PlaidItem>>(Items.Where(item => item.BankAccountId == bankAccountId).ToList());

    public Task SaveAsync(PlaidItem item, CancellationToken cancellationToken = default)
    {
        if (!Items.Contains(item))
        {
            Items.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(PlaidItem item, CancellationToken cancellationToken = default)
    {
        Items.Remove(item);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Records the webhooks it is handed, and throws when told to.
/// </summary>
internal sealed class RecordingPlaidWebhookHandler : IPlaidWebhookHandler
{
    public List<PlaidWebhookContext> Received { get; } = [];

    public Exception Failure { get; set; }

    public Task HandleAsync(PlaidWebhookContext context, CancellationToken cancellationToken = default)
    {
        Received.Add(context);

        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}

/// <summary>
/// The Plaid services over a fake handler.
/// </summary>
internal sealed class PlaidTestContext
{
    public PlaidTestContext()
    {
        Client = new PlaidClient(new FakeHttpClientFactory(Handler));
        Protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());
        Cleaner = new PlaidItemCleaner(Client, Credentials, Store, Protector, NullLogger<PlaidItemCleaner>.Instance);
    }

    public FakePlaidHandler Handler { get; } = new();

    public FixedPlaidCredentials Credentials { get; } = new();

    public InMemoryPlaidItemStore Store { get; } = new();

    public PlaidClient Client { get; }

    public ISensitiveValueProtector Protector { get; }

    public PlaidItemCleaner Cleaner { get; }
}
