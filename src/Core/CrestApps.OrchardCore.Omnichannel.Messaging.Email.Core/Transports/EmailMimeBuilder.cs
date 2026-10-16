using MimeKit;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Builds the MIME message a transport that sends headers delivers: both bodies, the files, and the headers that thread
/// the email and say what kind of mail it is.
/// </summary>
public static class EmailMimeBuilder
{
    /// <summary>
    /// Builds the MIME message for a composed email.
    /// </summary>
    /// <param name="message">The composed email.</param>
    /// <returns>The MIME message.</returns>
    public static MimeMessage Build(EmailTransportMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(message.FromName?.Trim() ?? string.Empty, message.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.ToAddress));

        if (!string.IsNullOrWhiteSpace(message.ReplyToAddress) &&
            !string.Equals(message.ReplyToAddress, message.FromAddress, StringComparison.OrdinalIgnoreCase))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyToAddress));
        }

        mime.Subject = message.Subject ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(message.MessageId))
        {
            mime.MessageId = message.MessageId;
        }

        if (!string.IsNullOrWhiteSpace(message.InReplyTo))
        {
            mime.InReplyTo = message.InReplyTo;
        }

        foreach (var reference in message.References.Where(reference => !string.IsNullOrWhiteSpace(reference)))
        {
            mime.References.Add(reference);
        }

        foreach (var header in message.Headers.Where(header => !string.IsNullOrWhiteSpace(header.Value)))
        {
            mime.Headers.Replace(header.Key, header.Value);
        }

        var body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        };

        foreach (var attachment in message.Attachments)
        {
            var contentType = ContentType.TryParse(attachment.ContentType ?? string.Empty, out var parsed)
                ? parsed
                : new ContentType("application", "octet-stream");

            body.Attachments.Add(string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : attachment.FileName, attachment.Content, contentType);
        }

        mime.Body = body.ToMessageBody();

        return mime;
    }
}
