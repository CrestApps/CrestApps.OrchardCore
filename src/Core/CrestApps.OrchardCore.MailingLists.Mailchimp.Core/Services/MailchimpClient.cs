using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// The Mailchimp Marketing API calls mailing lists make, as plain JSON over the named
/// <see cref="MailchimpConstants.HttpClientName"/> client. The API key goes in the <c>Authorization</c> header of each
/// request, never in a URL, a body, an exception or a log. At most <see cref="MailchimpConcurrencyGate.MaxConcurrentCalls"/>
/// calls of a tenant run at the same time.
/// </summary>
public sealed class MailchimpClient
{
    /// <summary>
    /// Marks a request that is safe to send again when it fails on the way: the resilience pipeline retries only those.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> RetryableKey = new("CrestApps.MailingLists.Mailchimp.Retryable");

    /// <summary>
    /// The largest page the members and lists endpoints return.
    /// </summary>
    public const int MaxPageSize = 1000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MailchimpConcurrencyGate _gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="gate">The tenant's gate that limits simultaneous calls.</param>
    public MailchimpClient(
        IHttpClientFactory httpClientFactory,
        MailchimpConcurrencyGate gate)
    {
        _httpClientFactory = httpClientFactory;
        _gate = gate;
    }

    /// <summary>
    /// Checks that the credentials work (<c>GET /ping</c>).
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public async Task PingAsync(MailchimpCredentials credentials, CancellationToken cancellationToken = default)
    {
        await SendAsync(credentials, HttpMethod.Get, "ping", body: null, retryable: true, allowNotFound: false, cancellationToken);
    }

    /// <summary>
    /// Reads the account's audiences (<c>GET /lists</c>), at most <see cref="MaxPageSize"/>.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The audiences, by name.</returns>
    public async Task<IReadOnlyList<MailchimpAudience>> GetAudiencesAsync(MailchimpCredentials credentials, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            credentials,
            HttpMethod.Get,
            $"lists?count={MaxPageSize}&fields=lists.id,lists.name",
            body: null,
            retryable: true,
            allowNotFound: false,
            cancellationToken);

        var audiences = new List<MailchimpAudience>();

        if (response.Body?["lists"] is JsonArray lists)
        {
            foreach (var item in lists)
            {
                var id = ReadString(item, "id");

                if (!string.IsNullOrEmpty(id))
                {
                    audiences.Add(new MailchimpAudience
                    {
                        Id = id,
                        Name = ReadString(item, "name") ?? id,
                    });
                }
            }
        }

        return audiences.OrderBy(audience => audience.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Creates or updates an audience member (<c>PUT /lists/{list_id}/members/{subscriber_hash}</c>). Naturally
    /// idempotent, so it is retried after a failure on the way.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="subscriberHash">The member's subscriber hash.</param>
    /// <param name="body">The member: <c>email_address</c>, <c>status_if_new</c>, <c>status</c>, <c>merge_fields</c>.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The member as Mailchimp holds it after the call: its id and status.</returns>
    public async Task<MailchimpMember> UpsertMemberAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string subscriberHash,
        JsonObject body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        var response = await SendAsync(credentials, HttpMethod.Put, MemberPath(audienceId, subscriberHash), body, retryable: true, allowNotFound: false, cancellationToken);

        return new MailchimpMember
        {
            Id = ReadString(response.Body, "id") ?? subscriberHash,
            EmailAddress = ReadString(response.Body, "email_address"),
            Status = ReadString(response.Body, "status"),
            LastChangedUtc = ReadUtc(ReadString(response.Body, "last_changed")),
        };
    }

    /// <summary>
    /// Changes the status of an existing audience member (<c>PATCH /lists/{list_id}/members/{subscriber_hash}</c>),
    /// without adding an address Mailchimp does not have.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="subscriberHash">The member's subscriber hash.</param>
    /// <param name="status">The new status, such as <c>unsubscribed</c>.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns><see langword="false"/> when the audience has no such member.</returns>
    public async Task<bool> UpdateMemberStatusAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string subscriberHash,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(status);

        var response = await SendAsync(
            credentials,
            HttpMethod.Patch,
            MemberPath(audienceId, subscriberHash),
            new JsonObject { ["status"] = status },
            retryable: true,
            allowNotFound: true,
            cancellationToken);

        return response.Found;
    }

    /// <summary>
    /// Makes tags active on an audience member (<c>POST /lists/{list_id}/members/{subscriber_hash}/tags</c>). Making a
    /// tag active twice changes nothing, so it is retried after a failure on the way.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="subscriberHash">The member's subscriber hash.</param>
    /// <param name="tags">The tag names.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public async Task AddTagsAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string subscriberHash,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var items = new JsonArray();

        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                items.Add(new JsonObject
                {
                    ["name"] = tag.Trim(),
                    ["status"] = "active",
                });
            }
        }

        if (items.Count == 0)
        {
            return;
        }

        await SendAsync(
            credentials,
            HttpMethod.Post,
            MemberPath(audienceId, subscriberHash) + "/tags",
            new JsonObject { ["tags"] = items },
            retryable: true,
            allowNotFound: false,
            cancellationToken);
    }

    /// <summary>
    /// Erases an audience member and every trace of it
    /// (<c>POST /lists/{list_id}/members/{subscriber_hash}/actions/delete-permanent</c>). Mailchimp never lets the
    /// address be imported again.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="subscriberHash">The member's subscriber hash.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns><see langword="false"/> when the audience has no such member (already erased, or never there).</returns>
    public async Task<bool> DeleteMemberPermanentlyAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string subscriberHash,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            credentials,
            HttpMethod.Post,
            MemberPath(audienceId, subscriberHash) + "/actions/delete-permanent",
            body: null,
            retryable: true,
            allowNotFound: true,
            cancellationToken);

        return response.Found;
    }

    /// <summary>
    /// Reads a page of the audience members that changed since a time
    /// (<c>GET /lists/{list_id}/members?since_last_changed=...</c>).
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="sinceUtc">The time (UTC).</param>
    /// <param name="offset">How many members to skip.</param>
    /// <param name="count">The page size, at most <see cref="MaxPageSize"/>.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The page.</returns>
    public async Task<MailchimpMemberPage> GetMembersChangedSinceAsync(
        MailchimpCredentials credentials,
        string audienceId,
        DateTime sinceUtc,
        int offset,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var since = (sinceUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc) : sinceUtc.ToUniversalTime())
            .ToString("yyyy-MM-dd'T'HH:mm:ss'+00:00'", CultureInfo.InvariantCulture);

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"{ListPath(audienceId)}/members?since_last_changed={Uri.EscapeDataString(since)}&count={Math.Min(count, MaxPageSize)}&offset={offset}&fields=total_items,members.id,members.email_address,members.status,members.last_changed,members.merge_fields");

        var response = await SendAsync(credentials, HttpMethod.Get, path, body: null, retryable: true, allowNotFound: false, cancellationToken);

        return ReadMemberPage(response.Body);
    }

    /// <summary>
    /// Reads the tags of the audience's merge fields (<c>GET /lists/{list_id}/merge-fields</c>).
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The tags.</returns>
    public async Task<IReadOnlyList<string>> GetMergeFieldTagsAsync(
        MailchimpCredentials credentials,
        string audienceId,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            credentials,
            HttpMethod.Get,
            $"{ListPath(audienceId)}/merge-fields?count={MaxPageSize}&fields=merge_fields.tag",
            body: null,
            retryable: true,
            allowNotFound: false,
            cancellationToken);

        var tags = new List<string>();

        if (response.Body?["merge_fields"] is JsonArray fields)
        {
            foreach (var field in fields)
            {
                var tag = ReadString(field, "tag");

                if (!string.IsNullOrEmpty(tag))
                {
                    tags.Add(tag);
                }
            }
        }

        return tags;
    }

    /// <summary>
    /// Adds a text merge field to the audience (<c>POST /lists/{list_id}/merge-fields</c>). Never retried: a second
    /// call would be refused because the tag exists.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="tag">The merge tag, such as <c>COUPON</c>.</param>
    /// <param name="name">The field's name in Mailchimp.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public async Task CreateTextMergeFieldAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string tag,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var body = new JsonObject
        {
            ["tag"] = tag.Trim(),
            ["name"] = name.Trim(),
            ["type"] = "text",
            ["required"] = false,
            ["public"] = false,
        };

        await SendAsync(credentials, HttpMethod.Post, $"{ListPath(audienceId)}/merge-fields", body, retryable: false, allowNotFound: false, cancellationToken);
    }

    /// <summary>
    /// Creates a webhook on the audience (<c>POST /lists/{list_id}/webhooks</c>) for the subscribe, unsubscribe,
    /// profile, address change and cleaned events, from people and admins but not from the API, so the site's own
    /// changes do not come back. Never retried: a second call would create a second webhook.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="url">The URL Mailchimp posts to.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The webhook's id and, when Mailchimp returns one, its signing secret.</returns>
    public async Task<MailchimpWebhookRegistration> CreateWebhookAsync(
        MailchimpCredentials credentials,
        string audienceId,
        Uri url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var body = new JsonObject
        {
            ["url"] = url.AbsoluteUri,
            ["events"] = new JsonObject
            {
                ["subscribe"] = true,
                ["unsubscribe"] = true,
                ["profile"] = true,
                ["cleaned"] = true,
                ["upemail"] = true,
                ["campaign"] = false,
            },
            ["sources"] = new JsonObject
            {
                ["user"] = true,
                ["admin"] = true,
                ["api"] = false,
            },
        };

        var response = await SendAsync(credentials, HttpMethod.Post, $"{ListPath(audienceId)}/webhooks", body, retryable: false, allowNotFound: false, cancellationToken);

        var id = ReadString(response.Body, "id");

        if (string.IsNullOrEmpty(id))
        {
            throw new MailchimpApiException(HttpStatusCode.OK, "Webhook Without Id", "Mailchimp created the webhook but returned no id.");
        }

        return new MailchimpWebhookRegistration
        {
            Id = id,
            SigningSecret = ReadString(response.Body, "signing_secret") ?? ReadString(response.Body, "secret"),
        };
    }

    /// <summary>
    /// Deletes a webhook of the audience (<c>DELETE /lists/{list_id}/webhooks/{webhook_id}</c>).
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="audienceId">The audience's id.</param>
    /// <param name="webhookId">The webhook's id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns><see langword="false"/> when the audience has no such webhook.</returns>
    public async Task<bool> DeleteWebhookAsync(
        MailchimpCredentials credentials,
        string audienceId,
        string webhookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(webhookId);

        var response = await SendAsync(
            credentials,
            HttpMethod.Delete,
            $"{ListPath(audienceId)}/webhooks/{Uri.EscapeDataString(webhookId)}",
            body: null,
            retryable: true,
            allowNotFound: true,
            cancellationToken);

        return response.Found;
    }

    internal static MailchimpMemberPage ReadMemberPage(JsonNode body)
    {
        var members = new List<MailchimpMember>();

        if (body?["members"] is JsonArray items)
        {
            foreach (var item in items)
            {
                if (item is not JsonObject member)
                {
                    continue;
                }

                var mergeFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (member["merge_fields"] is JsonObject fields)
                {
                    foreach (var (tag, value) in fields)
                    {
                        if (value is JsonValue text && text.TryGetValue<string>(out var stringValue))
                        {
                            mergeFields[tag] = stringValue;
                        }
                    }
                }

                members.Add(new MailchimpMember
                {
                    Id = ReadString(member, "id"),
                    EmailAddress = ReadString(member, "email_address"),
                    Status = ReadString(member, "status"),
                    LastChangedUtc = ReadUtc(ReadString(member, "last_changed")),
                    MergeFields = mergeFields,
                });
            }
        }

        var total = 0;

        if (body?["total_items"] is JsonValue totalValue && totalValue.TryGetValue<int>(out var totalItems))
        {
            total = totalItems;
        }

        return new MailchimpMemberPage
        {
            Members = members,
            TotalItems = total,
        };
    }

    private async Task<MailchimpResponse> SendAsync(
        MailchimpCredentials credentials,
        HttpMethod method,
        string path,
        JsonNode body,
        bool retryable,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        using var request = new HttpRequestMessage(method, new Uri(credentials.BaseAddress, path));

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Options.Set(RetryableKey, retryable);

        var client = _httpClientFactory.CreateClient(MailchimpConstants.HttpClientName);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return new MailchimpResponse(false, null);
            }

            var text = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
            var json = Parse(text);

            if (!response.IsSuccessStatusCode)
            {
                throw new MailchimpApiException(response.StatusCode, ReadString(json, "title"), ReadString(json, "detail"));
            }

            return new MailchimpResponse(true, json);
        }
        finally
        {
            _gate.Release();
        }
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
            // Not Mailchimp's JSON, such as a proxy's page; the status says enough.
            return null;
        }
    }

    private static string ReadString(JsonNode node, string name)
    {
        if (node is JsonObject item && item[name] is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    private static DateTime? ReadUtc(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        return null;
    }

    private static string ListPath(string audienceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audienceId);

        return "lists/" + Uri.EscapeDataString(audienceId.Trim());
    }

    private static string MemberPath(string audienceId, string subscriberHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriberHash);

        return ListPath(audienceId) + "/members/" + Uri.EscapeDataString(subscriberHash);
    }

    private readonly record struct MailchimpResponse(bool Found, JsonNode Body);
}
