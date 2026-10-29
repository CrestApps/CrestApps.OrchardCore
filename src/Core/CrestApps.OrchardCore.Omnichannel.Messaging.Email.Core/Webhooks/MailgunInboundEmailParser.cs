using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads a Mailgun Route's <c>forward()</c> action: form fields (<c>sender</c>, <c>recipient</c>, <c>subject</c>,
/// <c>body-plain</c>, <c>stripped-text</c>, <c>message-headers</c> and the files), or the whole message in
/// <c>body-mime</c> when the route forwards to a URL ending in <c>mime</c>. When the tenant has the Mailgun signing key,
/// the call must carry a valid signature no older than fifteen minutes.
/// </summary>
public sealed class MailgunInboundEmailParser : IInboundEmailWebhookParser
{
    private static readonly TimeSpan _maxSignatureAge = TimeSpan.FromMinutes(15);

    private readonly IEmailSecretProtector _secretProtector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailgunInboundEmailParser"/> class.
    /// </summary>
    /// <param name="secretProtector">The protector the signing key is read with.</param>
    /// <param name="clock">The clock the signature's age is judged by.</param>
    public MailgunInboundEmailParser(
        IEmailSecretProtector secretProtector,
        IClock clock)
    {
        _secretProtector = secretProtector;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.Mailgun;

    /// <inheritdoc/>
    public async Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The signature is checked before anything the caller chooses (the content type included) can end the call early,
        // so no shape of request reaches the email without passing it.
        var form = request.HasFormContentType ? await request.ReadFormAsync(cancellationToken) : FormCollection.Empty;
        var signingKey = _secretProtector.Unprotect(settings?.MailgunSigningKey);

        if (!string.IsNullOrEmpty(signingKey) &&
            !IsSignatureValid(signingKey, form["timestamp"].ToString(), form["token"].ToString(), form["signature"].ToString(), _clock.UtcNow))
        {
            return InboundEmailWebhookResult.Unauthorized("The Mailgun signature is missing, wrong or too old.");
        }

        if (!request.HasFormContentType)
        {
            return InboundEmailWebhookResult.Invalid("Mailgun posts inbound email as form data.");
        }

        InboundEmail email;

        var raw = form["body-mime"].ToString();

        if (!string.IsNullOrEmpty(raw))
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
            email = await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);
        }
        else
        {
            email = new InboundEmail
            {
                From = SendGridInboundEmailParser.ParseAddress(form["from"].ToString()) ?? SendGridInboundEmailParser.ParseAddress(form["sender"].ToString()),
                To = SendGridInboundEmailParser.ParseAddresses(form["To"].ToString()),
                Cc = SendGridInboundEmailParser.ParseAddresses(form["Cc"].ToString()),
                Subject = form["subject"].ToString(),
                TextBody = form["body-plain"].ToString(),
                HtmlBody = form["body-html"].ToString(),
                StrippedReply = form["stripped-text"].ToString(),
                MessageId = MimeInboundEmailParser.TrimMessageId(form["Message-Id"].ToString()),
                InReplyTo = MimeInboundEmailParser.TrimMessageId(form["In-Reply-To"].ToString()),
                References = MimeInboundEmailParser.ParseMessageIds(form["References"].ToString()),
            };

            InboundEmailHeaders.Apply(email, ReadHeaderPairs(form["message-headers"].ToString()));

            var contentIds = ReadContentIdMap(form["content-id-map"].ToString());

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

        foreach (var recipient in MimeInboundEmailParser.ParseAddressList(form["recipient"].ToString()).Where(recipient => !email.DeliveredTo.Contains(recipient, StringComparer.Ordinal)))
        {
            email.DeliveredTo.Add(recipient);
        }

        return InboundEmailWebhookResult.Of(email);
    }

    /// <summary>
    /// Checks a Mailgun webhook signature: the hex HMAC-SHA256 of the timestamp and the token, keyed with the signing key.
    /// </summary>
    /// <param name="signingKey">The tenant's Mailgun HTTP webhook signing key.</param>
    /// <param name="timestamp">The call's timestamp, in Unix seconds.</param>
    /// <param name="token">The call's random token.</param>
    /// <param name="signature">The call's signature.</param>
    /// <param name="utcNow">The current time, against which the timestamp's age is judged.</param>
    /// <returns><see langword="true"/> when the signature is genuine and recent.</returns>
    public static bool IsSignatureValid(string signingKey, string timestamp, string token, string signature, DateTime utcNow)
    {
        if (string.IsNullOrEmpty(signingKey) ||
            string.IsNullOrEmpty(token) ||
            string.IsNullOrEmpty(signature) ||
            !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        DateTime signedUtc;

        try
        {
            signedUtc = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if (utcNow - signedUtc > _maxSignatureAge || signedUtc - utcNow > TimeSpan.FromMinutes(2))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(timestamp + token));

        byte[] provided;

        try
        {
            provided = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    // [["Received","by ..."],["Message-Id","<...>"]]
    private static List<KeyValuePair<string, string>> ReadHeaderPairs(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.EnumerateArray()
                .Where(pair => pair.ValueKind == JsonValueKind.Array && pair.GetArrayLength() == 2)
                .Select(pair => KeyValuePair.Create(pair[0].GetString(), pair[1].GetString()))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    // {"<ii_abc>":"attachment-1"}
    private static Dictionary<string, string> ReadContentIdMap(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    map[property.Value.GetString()] = MimeInboundEmailParser.TrimMessageId(property.Name);
                }
            }
        }
        catch (JsonException)
        {
            // Without the map every file is kept as an attachment.
        }

        return map;
    }
}
