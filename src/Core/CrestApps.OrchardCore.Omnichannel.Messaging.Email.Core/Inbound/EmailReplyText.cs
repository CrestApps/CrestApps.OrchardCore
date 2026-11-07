using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Separates what a customer just wrote from the history their mail client quoted below it, so the thread shows the new
/// reply and folds the rest. It recognises the quote headers the common mail clients write (Gmail, Outlook, Apple Mail,
/// Thunderbird, Yahoo) and lines quoted with <c>&gt;</c>.
/// </summary>
public static partial class EmailReplyText
{
    /// <summary>
    /// Splits an email body into the reply and the quoted history.
    /// </summary>
    /// <param name="body">The plain-text body.</param>
    /// <returns>The reply, and the quoted history (empty when nothing was quoted).</returns>
    public static (string Reply, string Quoted) Split(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (string.Empty, string.Empty);
        }

        var lines = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var cut = FindQuoteStart(lines);

        if (cut < 0)
        {
            return (body.Trim(), string.Empty);
        }

        var reply = string.Join('\n', lines.Take(cut)).Trim();
        var quoted = string.Join('\n', lines.Skip(cut)).Trim();

        // A message that is nothing but a quote (a forward with no note) keeps the quote as its body, so the thread does
        // not show an empty message.
        return string.IsNullOrEmpty(reply)
            ? (quoted, string.Empty)
            : (reply, quoted);
    }

    private static int FindQuoteStart(string[] lines)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            // "On Tue, 3 Sep 2026 at 10:01, Ann <ann@example.com> wrote:", possibly wrapped over two lines.
            if (WroteHeader().IsMatch(line) ||
                (index + 1 < lines.Length && WroteHeader().IsMatch($"{line} {lines[index + 1].Trim()}") && line.StartsWith("On ", StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }

            if (OriginalMessageSeparator().IsMatch(line) || ForwardedSeparator().IsMatch(line))
            {
                return index;
            }

            // Outlook's header block: "From: ..." followed within a few lines by "Sent:" or "Date:" and "To:" or "Subject:".
            if (OutlookFrom().IsMatch(line) && IsOutlookHeaderBlock(lines, index))
            {
                return index;
            }

            // An underscore rule Outlook puts above the header block.
            if (UnderscoreRule().IsMatch(line) && index + 1 < lines.Length && OutlookFrom().IsMatch(lines[index + 1].Trim()))
            {
                return index;
            }

            // A run of lines quoted with ">" that lasts to the end (or to a signature) is the history.
            if (line.StartsWith('>') && IsQuotedToEnd(lines, index))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsOutlookHeaderBlock(string[] lines, int start)
    {
        var seenDate = false;
        var seenRecipientOrSubject = false;

        for (var index = start + 1; index < Math.Min(lines.Length, start + 6); index++)
        {
            var line = lines[index].Trim();

            seenDate |= OutlookDate().IsMatch(line);
            seenRecipientOrSubject |= OutlookToOrSubject().IsMatch(line);
        }

        return seenDate && seenRecipientOrSubject;
    }

    private static bool IsQuotedToEnd(string[] lines, int start)
    {
        for (var index = start; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.Length == 0 || line.StartsWith('>'))
            {
                continue;
            }

            // Text after the quote is an answer written between quoted lines (inline replies), so the quote is not just
            // history and is kept.
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^(On|Le|Am|El|Il|Op|Em|Den|På)\s.+(wrote|a écrit|schrieb|escribió|ha scritto|schreef|escreveu|skrev)\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WroteHeader();

    [GeneratedRegex(@"^-{2,}\s*(Original Message|Message d'origine|Ursprüngliche Nachricht|Mensaje original|Messaggio originale)\s*-{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex OriginalMessageSeparator();

    [GeneratedRegex(@"^-{2,}\s*Forwarded message\s*-{2,}$|^Begin forwarded message:$", RegexOptions.IgnoreCase)]
    private static partial Regex ForwardedSeparator();

    [GeneratedRegex(@"^\*?(From|De|Von|Da|Van)\s*:\*?\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex OutlookFrom();

    [GeneratedRegex(@"^\*?(Sent|Date|Envoyé|Gesendet|Enviado|Inviato|Verzonden)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex OutlookDate();

    [GeneratedRegex(@"^\*?(To|Subject|À|Objet|An|Betreff|Para|Asunto|A|Oggetto|Aan|Onderwerp)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex OutlookToOrSubject();

    [GeneratedRegex(@"^_{10,}$")]
    private static partial Regex UnderscoreRule();
}
