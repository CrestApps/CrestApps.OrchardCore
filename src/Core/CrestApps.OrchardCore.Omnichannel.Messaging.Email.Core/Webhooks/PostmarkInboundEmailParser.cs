using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads Postmark inbound processing: a JSON post with <c>FromFull</c>, <c>ToFull</c>, <c>CcFull</c>,
/// <c>OriginalRecipient</c>, <c>Subject</c>, <c>TextBody</c>, <c>HtmlBody</c>, <c>StrippedTextReply</c>, the
/// <c>Headers</c> (which carry the email's own <c>Message-ID</c>) and base64 <c>Attachments</c>.
/// </summary>
public sealed class PostmarkInboundEmailParser : IInboundEmailWebhookParser
{
    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.Postmark;

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
            return InboundEmailWebhookResult.Invalid("The Postmark payload is not valid JSON.");
        }

        using (document)
        {
            return InboundEmailWebhookResult.Of(Parse(document.RootElement));
        }
    }

    /// <summary>
    /// Reads a Postmark inbound payload.
    /// </summary>
    /// <param name="root">The payload's root object.</param>
    /// <returns>The email.</returns>
    public static InboundEmail Parse(JsonElement root)
    {
        var email = new InboundEmail
        {
            From = ReadFull(root, "FromFull") ?? SendGridInboundEmailParser.ParseAddress(GetString(root, "From")),
            To = ReadFullList(root, "ToFull"),
            Cc = ReadFullList(root, "CcFull"),
            Subject = GetString(root, "Subject"),
            TextBody = GetString(root, "TextBody"),
            HtmlBody = GetString(root, "HtmlBody"),
            StrippedReply = GetString(root, "StrippedTextReply"),
        };

        var originalRecipient = new InboundEmailAddress(GetString(root, "OriginalRecipient")).Address;

        if (originalRecipient is not null)
        {
            email.DeliveredTo.Add(originalRecipient);
        }

        if (DateTimeOffset.TryParse(GetString(root, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            email.Date = date;
        }

        if (root.TryGetProperty("Headers", out var headers) && headers.ValueKind == JsonValueKind.Array)
        {
            InboundEmailHeaders.Apply(email, headers.EnumerateArray()
                .Where(header => header.ValueKind == JsonValueKind.Object)
                .Select(header => KeyValuePair.Create(GetString(header, "Name"), GetString(header, "Value")))
                .ToList());
        }

        if (root.TryGetProperty("Attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
        {
            foreach (var attachment in attachments.EnumerateArray())
            {
                var content = GetString(attachment, "Content");

                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                try
                {
                    email.Attachments.Add(new InboundEmailAttachment
                    {
                        FileName = GetString(attachment, "Name"),
                        ContentType = GetString(attachment, "ContentType"),
                        Content = Convert.FromBase64String(content),
                        ContentId = MimeInboundEmailParser.TrimMessageId(GetString(attachment, "ContentID")),
                    });
                }
                catch (FormatException)
                {
                    // A file that is not valid base64 is left out.
                }
            }
        }

        return email;
    }

    private static InboundEmailAddress ReadFull(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? ToAddress(value)
            : null;

    private static List<InboundEmailAddress> ReadFullList(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(ToAddress).Where(address => address?.Address is not null).ToList()
            : [];

    private static InboundEmailAddress ToAddress(JsonElement value)
    {
        var address = new InboundEmailAddress(GetString(value, "Email"), GetString(value, "Name"));

        return address.Address is null ? null : address;
    }

    private static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
