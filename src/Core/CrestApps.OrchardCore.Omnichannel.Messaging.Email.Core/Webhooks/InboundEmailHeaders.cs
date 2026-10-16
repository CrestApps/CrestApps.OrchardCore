using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using MimeKit;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Applies the headers a provider passes along beside its parsed fields (a raw header block, or name and value pairs) to
/// an <see cref="InboundEmail"/>: the threading identifiers and the headers that mark automatic mail.
/// </summary>
public static class InboundEmailHeaders
{
    /// <summary>
    /// Applies a raw header block, as SendGrid sends it.
    /// </summary>
    /// <param name="email">The email the headers belong to.</param>
    /// <param name="headerBlock">The raw headers, one per line.</param>
    public static void ApplyBlock(InboundEmail email, string headerBlock)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(headerBlock))
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(headerBlock.TrimEnd() + "\r\n\r\n"));
            var headers = HeaderList.Load(stream);

            Apply(email, headers.Select(header => KeyValuePair.Create(header.Field, header.Value)));
        }
        catch (FormatException)
        {
            // A header block that cannot be read leaves the parsed fields as they are.
        }
    }

    /// <summary>
    /// Applies name and value pairs, as Mailgun and Postmark send them.
    /// </summary>
    /// <param name="email">The email the headers belong to.</param>
    /// <param name="headers">The headers.</param>
    public static void Apply(InboundEmail email, IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (headers is null)
        {
            return;
        }

        var parsed = MimeInboundEmailParser.Parse(BuildMessage(headers));

        email.MessageId ??= parsed.MessageId;
        email.InReplyTo ??= parsed.InReplyTo;

        if (email.References.Count == 0)
        {
            email.References = parsed.References;
        }

        foreach (var address in parsed.DeliveredTo.Where(address => !email.DeliveredTo.Contains(address, StringComparer.Ordinal)))
        {
            email.DeliveredTo.Add(address);
        }

        foreach (var header in parsed.Headers)
        {
            email.Headers.TryAdd(header.Key, header.Value);
        }

        email.Date ??= parsed.Date;
    }

    private static MimeMessage BuildMessage(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var message = new MimeMessage();

        // A new message stamps its own Message-ID, Date and MIME-Version, which would hide the email's own.
        message.Headers.Clear();

        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name) || value is null)
            {
                continue;
            }

            try
            {
                message.Headers.Add(name.Trim(), value);
            }
            catch (ArgumentException)
            {
                // A header name the MIME rules do not allow is skipped.
            }
        }

        return message;
    }
}
