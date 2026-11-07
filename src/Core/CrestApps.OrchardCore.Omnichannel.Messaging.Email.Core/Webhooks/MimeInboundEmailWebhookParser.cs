using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads a raw RFC 822 message posted by anything that can relay mail: a Cloudflare Email Worker, an AWS Lambda behind
/// SES, a mail server's pipe-to-script, or a provider that forwards the raw message. The body is the message itself; a
/// form post may carry it as an <c>email</c>, <c>mime</c> or <c>message</c> field or file instead. The envelope recipient
/// may be passed as the <c>to</c> query value or the <c>X-Envelope-To</c> header.
/// </summary>
public sealed class MimeInboundEmailWebhookParser : IInboundEmailWebhookParser
{
    private static readonly string[] _formFields = ["email", "mime", "message"];

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.Mime;

    /// <inheritdoc/>
    public async Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        InboundEmail email;

        try
        {
            email = request.HasFormContentType
                ? await ParseFormAsync(request, cancellationToken)
                : await MimeInboundEmailParser.ParseAsync(request.Body, cancellationToken);
        }
        catch (FormatException)
        {
            return InboundEmailWebhookResult.Invalid("The posted message is not a valid RFC 822 message.");
        }

        if (email is null)
        {
            return InboundEmailWebhookResult.Invalid("The form carries no message in an email, mime or message field.");
        }

        var envelope = MimeInboundEmailParser.ParseAddressList(request.Query["to"].ToString())
            .Concat(MimeInboundEmailParser.ParseAddressList(request.Headers["X-Envelope-To"].ToString()));

        foreach (var recipient in envelope.Where(recipient => !email.DeliveredTo.Contains(recipient, StringComparer.Ordinal)))
        {
            email.DeliveredTo.Add(recipient);
        }

        return InboundEmailWebhookResult.Of(email);
    }

    private static async Task<InboundEmail> ParseFormAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);

        foreach (var field in _formFields)
        {
            var file = form.Files.GetFile(field);

            if (file is not null)
            {
                await using var stream = file.OpenReadStream();

                return await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);
            }

            var value = form[field].ToString();

            if (!string.IsNullOrEmpty(value))
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));

                return await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);
            }
        }

        return null;
    }
}
