using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

/// <summary>
/// Answers Mailchimp calls by method and path, in order, and records every request with its body.
/// </summary>
internal sealed class FakeMailchimpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _answers = new(StringComparer.Ordinal);

    public List<(HttpMethod Method, string Path, HttpRequestMessage Request, JsonObject Body)> Calls { get; } = [];

    public FakeMailchimpHandler Answer(HttpMethod method, string path, string body, HttpStatusCode status = HttpStatusCode.OK)
        => Enqueue(method, path, () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json"),
        });

    public FakeMailchimpHandler Problem(HttpMethod method, string path, HttpStatusCode status, string title, string detail)
        => Answer(method, path, new JsonObject
        {
            ["type"] = "https://mailchimp.com/developer/marketing/docs/errors/",
            ["title"] = title,
            ["status"] = (int)status,
            ["detail"] = detail,
            ["instance"] = "instance-1",
        }.ToJsonString(), status);

    public FakeMailchimpHandler Throw(HttpMethod method, string path, Exception exception)
        => Enqueue(method, path, () => throw exception);

    public IEnumerable<(HttpMethod Method, string Path, HttpRequestMessage Request, JsonObject Body)> CallsTo(HttpMethod method, string path)
        => Calls.Where(call => call.Method == method && call.Path == path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var text = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var body = string.IsNullOrEmpty(text) ? null : JsonNode.Parse(text) as JsonObject;

        Calls.Add((request.Method, path, request, body));

        if (!_answers.TryGetValue(Key(request.Method, path), out var queue) || queue.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"title":"Resource Not Found","status":404}""", Encoding.UTF8, "application/json"),
            };
        }

        var answer = queue.Count == 1 ? queue.Peek() : queue.Dequeue();

        return answer();
    }

    private FakeMailchimpHandler Enqueue(HttpMethod method, string path, Func<HttpResponseMessage> answer)
    {
        var key = Key(method, path);

        if (!_answers.TryGetValue(key, out var queue))
        {
            _answers[key] = queue = new Queue<Func<HttpResponseMessage>>();
        }

        queue.Enqueue(answer);

        return this;
    }

    private static string Key(HttpMethod method, string path)
        => $"{method.Method} {path}";
}

/// <summary>
/// Hands out clients over one handler.
/// </summary>
internal sealed class FakeMailchimpHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeMailchimpHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false);
}

/// <summary>
/// Returns fixed credentials, or none.
/// </summary>
internal sealed class FixedMailchimpCredentials : IMailchimpCredentialsProvider
{
    public static readonly MailchimpCredentials Us21 = MailchimpCredentials.Create("key-123-us21", null);

    private readonly MailchimpCredentials _credentials;

    public FixedMailchimpCredentials(MailchimpCredentials credentials)
    {
        _credentials = credentials;
    }

    public Task<MailchimpCredentials> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_credentials);
}

/// <summary>
/// Builds a Mailchimp provider over a fake handler.
/// </summary>
internal sealed class MailchimpTestContext : IDisposable
{
    public const string AudienceId = "aud123";

    public MailchimpTestContext(MailchimpCredentials credentials = null, bool configured = true)
    {
        Gate = new MailchimpConcurrencyGate();
        Client = new MailchimpClient(new FakeMailchimpHttpClientFactory(Handler), Gate);
        Provider = new MailchimpMailingListProvider(
            new FixedMailchimpCredentials(configured ? credentials ?? FixedMailchimpCredentials.Us21 : null),
            Client,
            new StringLocalizer<MailchimpMailingListProvider>(new SourceTextLocalizerFactory()));
    }

    public FakeMailchimpHandler Handler { get; } = new();

    public MailchimpConcurrencyGate Gate { get; }

    public MailchimpClient Client { get; }

    public MailchimpMailingListProvider Provider { get; }

    public static string MemberPath(string email)
        => $"/3.0/lists/{AudienceId}/members/{MailchimpSubscriberHash.Compute(email)}";

    public static MailingList List(MailingListConfirmation confirmation = MailingListConfirmation.Email, Action<MailchimpListSettings> configure = null)
    {
        var list = new MailingList
        {
            ItemId = "list-1",
            Name = "news",
            DisplayText = "News",
            ProviderName = "Mailchimp",
            Confirmation = confirmation,
        };

        var settings = new MailchimpListSettings
        {
            AudienceId = AudienceId,
        };

        configure?.Invoke(settings);
        list.Put(settings);

        return list;
    }

    public static MailingListMember Member(string email, MailingListMemberStatus status, string name = null)
        => new()
        {
            ItemId = "member-1",
            ListId = "list-1",
            Contact = new ContactInfo
            {
                Email = email,
                Name = name,
            },
            NormalizedEmail = email.Trim().ToLowerInvariant(),
            Status = status,
        };

    public void Dispose()
        => Gate.Dispose();
}
