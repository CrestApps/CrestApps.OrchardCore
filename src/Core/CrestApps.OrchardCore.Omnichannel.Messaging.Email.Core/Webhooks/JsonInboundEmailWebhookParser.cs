using System.Globalization;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads the channel's own JSON shape, for automation platforms that can post JSON but not a provider's format: Power
/// Automate ("When a new email arrives" in Microsoft 365), Zapier, Make, n8n or a Google Apps Script on a Gmail inbox.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "from": "Ann Lee &lt;ann@example.com&gt;",
///   "to": ["support@contoso.com"],
///   "cc": [],
///   "subject": "Order 1042",
///   "text": "Hi, ...",
///   "html": "&lt;p&gt;Hi, ...&lt;/p&gt;",
///   "messageId": "&lt;CAF1...@mail.gmail.com&gt;",
///   "inReplyTo": "...",
///   "references": ["..."],
///   "date": "2026-10-09T14:03:00Z",
///   "headers": { "Auto-Submitted": "no" },
///   "attachments": [ { "fileName": "invoice.pdf", "contentType": "application/pdf", "content": "&lt;base64&gt;" } ],
///   "raw": "&lt;optional: the whole RFC 822 message, as text or base64&gt;"
/// }
/// </code>
/// When <c>raw</c> is present the message is read from it and the other fields only add to it.
/// </remarks>
public sealed class JsonInboundEmailWebhookParser : IInboundEmailWebhookParser
{
    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.Json;

    /// <inheritdoc/>
    public async Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonDocument document;

        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return InboundEmailWebhookResult.Invalid("The payload is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var emails = new List<InboundEmail>();

                foreach (var item in root.EnumerateArray())
                {
                    emails.Add(await ParseAsync(item, cancellationToken));
                }

                return new InboundEmailWebhookResult { Emails = emails };
            }

            return root.ValueKind == JsonValueKind.Object
                ? InboundEmailWebhookResult.Of(await ParseAsync(root, cancellationToken))
                : InboundEmailWebhookResult.Invalid("The payload must be an email object or an array of them.");
        }
    }

    /// <summary>
    /// Reads one email object.
    /// </summary>
    /// <param name="item">The email object.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The email.</returns>
    public static async Task<InboundEmail> ParseAsync(JsonElement item, CancellationToken cancellationToken = default)
    {
        InboundEmail email;

        var raw = GetString(item, "raw");

        if (!string.IsNullOrWhiteSpace(raw))
        {
            using var stream = new MemoryStream(DecodeRaw(raw));
            email = await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);
        }
        else
        {
            email = new InboundEmail
            {
                From = SendGridInboundEmailParser.ParseAddress(GetString(item, "from")),
                Subject = GetString(item, "subject"),
                TextBody = GetString(item, "text"),
                HtmlBody = GetString(item, "html"),
                MessageId = MimeInboundEmailParser.TrimMessageId(GetString(item, "messageId")),
                InReplyTo = MimeInboundEmailParser.TrimMessageId(GetString(item, "inReplyTo")),
            };

            email.To = ReadAddresses(item, "to");
            email.Cc = ReadAddresses(item, "cc");
            email.References = ReadStrings(item, "references").Select(MimeInboundEmailParser.TrimMessageId).Where(id => id is not null).ToList();

            if (DateTimeOffset.TryParse(GetString(item, "date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                email.Date = date;
            }

            if (item.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
            {
                InboundEmailHeaders.Apply(email, headers.EnumerateObject()
                    .Where(header => header.Value.ValueKind == JsonValueKind.String)
                    .Select(header => KeyValuePair.Create(header.Name, header.Value.GetString()))
                    .ToList());
            }

            ReadAttachments(item, email);
        }

        return email;
    }

    private static void ReadAttachments(JsonElement item, InboundEmail email)
    {
        if (!item.TryGetProperty("attachments", out var attachments) || attachments.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var attachment in attachments.EnumerateArray())
        {
            var content = GetString(attachment, "content");

            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            try
            {
                email.Attachments.Add(new InboundEmailAttachment
                {
                    FileName = GetString(attachment, "fileName") ?? GetString(attachment, "name"),
                    ContentType = GetString(attachment, "contentType"),
                    Content = Convert.FromBase64String(content),
                    ContentId = MimeInboundEmailParser.TrimMessageId(GetString(attachment, "contentId")),
                });
            }
            catch (FormatException)
            {
                // A file that is not valid base64 is left out.
            }
        }
    }

    // The raw message may be posted as the text itself or base64-encoded, as automation platforms tend to do.
    private static byte[] DecodeRaw(string raw)
    {
        var trimmed = raw.Trim();

        if (!trimmed.Contains(':', StringComparison.Ordinal) && trimmed.Length % 4 == 0)
        {
            try
            {
                return Convert.FromBase64String(trimmed);
            }
            catch (FormatException)
            {
                // Not base64 after all; read as text below.
            }
        }

        return Encoding.UTF8.GetBytes(raw);
    }

    private static List<InboundEmailAddress> ReadAddresses(JsonElement item, string name)
        => ReadStrings(item, name)
            .SelectMany(SendGridInboundEmailParser.ParseAddresses)
            .ToList();

    private static List<string> ReadStrings(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()],
            JsonValueKind.Array => value.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.String).Select(entry => entry.GetString()).ToList(),
            _ => [],
        };
    }

    private static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
