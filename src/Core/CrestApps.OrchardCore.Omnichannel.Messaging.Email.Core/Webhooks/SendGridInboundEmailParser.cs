using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads SendGrid Inbound Parse: a <c>multipart/form-data</c> post with either the parsed fields (<c>from</c>,
/// <c>to</c>, <c>subject</c>, <c>text</c>, <c>html</c>, <c>headers</c>, <c>envelope</c> and the files) or, when "POST the
/// raw, full MIME message" is ticked, the whole message in <c>email</c>, which is preferred because nothing is lost.
/// </summary>
public sealed class SendGridInboundEmailParser : IInboundEmailWebhookParser
{
    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.SendGrid;

    /// <inheritdoc/>
    public async Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasFormContentType)
        {
            return InboundEmailWebhookResult.Invalid("SendGrid posts inbound email as form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var envelopeRecipients = ReadEnvelopeRecipients(form["envelope"].ToString());

        InboundEmail email;

        var raw = form["email"].ToString();

        if (!string.IsNullOrEmpty(raw))
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
            email = await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);
        }
        else
        {
            email = new InboundEmail
            {
                From = ParseAddress(form["from"].ToString()),
                To = ParseAddresses(form["to"].ToString()),
                Cc = ParseAddresses(form["cc"].ToString()),
                Subject = form["subject"].ToString(),
                TextBody = form["text"].ToString(),
                HtmlBody = form["html"].ToString(),
            };

            InboundEmailHeaders.ApplyBlock(email, form["headers"].ToString());

            var contentIds = ReadContentIds(form["attachment-info"].ToString());

            foreach (var file in form.Files)
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);

                email.Attachments.Add(new InboundEmailAttachment
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Content = buffer.ToArray(),
                    ContentId = contentIds.TryGetValue(file.Name, out var contentId) ? contentId : null,
                });
            }
        }

        foreach (var recipient in envelopeRecipients.Where(recipient => !email.DeliveredTo.Contains(recipient, StringComparer.Ordinal)))
        {
            email.DeliveredTo.Add(recipient);
        }

        return InboundEmailWebhookResult.Of(email);
    }

    internal static InboundEmailAddress ParseAddress(string value)
    {
        var addresses = ParseAddresses(value);

        return addresses.Count > 0 ? addresses[0] : null;
    }

    internal static IList<InboundEmailAddress> ParseAddresses(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (MimeKit.InternetAddressList.TryParse(value, out var list))
        {
            return list.Mailboxes
                .Select(mailbox => new InboundEmailAddress(mailbox.Address, mailbox.Name))
                .Where(address => address.Address is not null)
                .ToList();
        }

        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(candidate => new InboundEmailAddress(candidate))
            .Where(address => address.Address is not null)
            .ToList();
    }

    // {"to":["support@contoso.com"],"from":"ann@example.com"}: the addresses SendGrid actually delivered to.
    private static List<string> ReadEnvelopeRecipients(string envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(envelope);

            if (document.RootElement.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array)
            {
                return to.EnumerateArray()
                    .Select(item => new InboundEmailAddress(item.GetString()).Address)
                    .Where(address => address is not null)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // An envelope that cannot be read leaves the header recipients to name the address.
        }

        return [];
    }

    // {"attachment1":{"filename":"logo.png","type":"image/png","content-id":"ii_abc"}}
    private static Dictionary<string, string> ReadContentIds(string attachmentInfo)
    {
        var contentIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(attachmentInfo))
        {
            return contentIds;
        }

        try
        {
            using var document = JsonDocument.Parse(attachmentInfo);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.TryGetProperty("content-id", out var contentId) &&
                    contentId.ValueKind == JsonValueKind.String)
                {
                    contentIds[property.Name] = MimeInboundEmailParser.TrimMessageId(contentId.GetString());
                }
            }
        }
        catch (JsonException)
        {
            // Without the attachment details every file is kept as an attachment.
        }

        return contentIds;
    }
}
