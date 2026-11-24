using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Omnichannel.Core;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// What a failed email send means for what happens next.
/// </summary>
public enum EmailFailureKind
{
    /// <summary>
    /// Worth another try later: a server that was busy, unreachable or broke off.
    /// </summary>
    Temporary = 0,

    /// <summary>
    /// The server asked the sender to slow down. The address's bulk mail pauses and the message is retried later.
    /// </summary>
    Throttled = 1,

    /// <summary>
    /// The receiving system refused the mail because of the sender: policy, reputation, authentication. Suppressing
    /// the recipient would be wrong; the address's bulk mail pauses instead.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The recipient's address does not exist or no longer takes mail. It is never tried again.
    /// </summary>
    HardBounce = 3,

    /// <summary>
    /// The receiving system refused this one email for what it is: too large, its content, or how it was sent (5.3.x,
    /// 5.5.x, 5.6.x). The email failed, but the recipient is not suppressed and the address's bulk mail does not pause.
    /// </summary>
    Rejected = 4,
}

/// <summary>
/// Reads a failed send's answer (the SMTP code, the enhanced status code, and the words around them) and decides what
/// it means. The enhanced code decides when there is one: 5.0.0, 5.1.x, 5.2.1 and 5.4.x are a dead address, 5.7.x a
/// block, 5.3.x, 5.5.x and 5.6.x a refusal of the one email, 4.7.x and the rate words a throttle. A bare 550 is only a
/// dead address when the words say so, because mail servers also answer 550 to blocks.
/// </summary>
public static partial class EmailFailureClassifier
{
    private static readonly string[] _throttleWords =
    [
        "rate limit",
        "ratelimit",
        "too many",
        "throttl",
        "slow down",
        "try again later",
        "temporarily deferred",
        "exceeded",
        "429",
        "toomanyrequests",
        "quota",
    ];

    // How a full mailbox is described. "Quota" and "exceeded" also describe a sender's own limits, so these words keep a
    // full mailbox, which is about the one recipient, from being read as a throttle that pauses all the address's mail.
    private static readonly string[] _fullMailboxWords =
    [
        "mailbox full",
        "mailbox is full",
        "mailbox quota",
        "mailbox size",
        "over quota",
        "overquota",
        "inbox is full",
        "storage allocation",
        "insufficient storage",
        "out of storage",
    ];

    private static readonly string[] _unknownRecipientWords =
    [
        "user unknown",
        "unknown user",
        "unknown recipient",
        "no such user",
        "no such recipient",
        "does not exist",
        "doesn't exist",
        "recipient not found",
        "mailbox not found",
        "mailbox unavailable",
        "invalid recipient",
        "address rejected",
        "recipient rejected",
        "not a valid mailbox",
        "account disabled",
        "account has been disabled",
        "mailbox disabled",
        "no mailbox",
    ];

    private static readonly string[] _blockWords =
    [
        "blocked",
        "block list",
        "blocklist",
        "blacklist",
        "spam",
        "reputation",
        "policy",
        "rejected due to",
        "not authorized",
        "unauthenticated",
        "dmarc",
        "spf",
        "dkim",
        "denied",
    ];

    /// <summary>
    /// Classifies a failed send.
    /// </summary>
    /// <param name="errorCode">The provider-neutral error code the transport set, when it set one.</param>
    /// <param name="text">The error text, which carries the server's codes and words.</param>
    /// <returns>What the failure means.</returns>
    public static EmailFailureKind Classify(string errorCode, string text)
    {
        text ??= string.Empty;

        var enhanced = EnhancedStatusPattern().Match(text);

        if (enhanced.Success)
        {
            var kind = ClassifyEnhanced(enhanced.Groups[1].Value, enhanced.Groups[2].Value, enhanced.Groups[3].Value, text);

            if (kind is not null)
            {
                return kind.Value;
            }
        }

        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, _throttleWords) && !ContainsAny(lower, _fullMailboxWords))
        {
            return EmailFailureKind.Throttled;
        }

        var isPermanent = string.Equals(errorCode, OmnichannelConstants.MessagingErrorCodes.RecipientRejected, StringComparison.Ordinal) ||
            BasicPermanentPattern().IsMatch(text);

        if (isPermanent)
        {
            // A permanent answer is about the sender as often as about the recipient; only the words tell them apart.
            if (ContainsAny(lower, _blockWords) && !ContainsAny(lower, _unknownRecipientWords))
            {
                return EmailFailureKind.Blocked;
            }

            if (ContainsAny(lower, _unknownRecipientWords) ||
                string.Equals(errorCode, OmnichannelConstants.MessagingErrorCodes.RecipientRejected, StringComparison.Ordinal))
            {
                return EmailFailureKind.HardBounce;
            }

            return EmailFailureKind.Blocked;
        }

        return EmailFailureKind.Temporary;
    }

    /// <summary>
    /// Classifies an enhanced status code (RFC 3463), such as <c>5.1.1</c>.
    /// </summary>
    /// <param name="status">The code.</param>
    /// <returns>What it means, or <see langword="null"/> when it is not a failure code.</returns>
    public static EmailFailureKind? ClassifyStatus(string status)
        => ClassifyStatus(status, status);

    /// <summary>
    /// Classifies an enhanced status code (RFC 3463), reading the words that came with it where the code alone does not
    /// decide, such as a 5.5.0 that says the mailbox does not exist.
    /// </summary>
    /// <param name="status">The code.</param>
    /// <param name="text">The words the code came with, such as a bounce report's reason, or <see langword="null"/>.</param>
    /// <returns>What it means, or <see langword="null"/> when it is not a failure code.</returns>
    internal static EmailFailureKind? ClassifyStatus(string status, string text)
    {
        var match = EnhancedStatusPattern().Match(status ?? string.Empty);

        return match.Success
            ? ClassifyEnhanced(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, text ?? status)
            : null;
    }

    /// <summary>
    /// Gets whether a failure is a temporary (4.x.x or 4xx) one: the receiving system will be asked again, so the
    /// failure is a deferral, not a bounce.
    /// </summary>
    /// <param name="status">The status code the failure was reported with, or <see langword="null"/>.</param>
    /// <param name="text">The words the failure was reported with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for a temporary failure code.</returns>
    internal static bool IsTemporaryStatus(string status, string text)
    {
        var match = EnhancedStatusPattern().Match(status ?? string.Empty);

        if (!match.Success)
        {
            match = EnhancedStatusPattern().Match(text ?? string.Empty);
        }

        return match.Success
            ? match.Groups[1].Value == "4"
            : BasicTemporaryPattern().IsMatch(status ?? string.Empty);
    }

    private static EmailFailureKind? ClassifyEnhanced(string severity, string subject, string detail, string text)
    {
        var lower = text?.ToLowerInvariant() ?? string.Empty;

        if (severity == "4")
        {
            // 4.7.x is the security and policy class, which is how mail servers say "slow down"; 4.2.1 and 4.4.5 are
            // mailbox and system congestion. The rest of 4.2.x is about the recipient's mailbox (4.2.2 is a full one),
            // so the words that come with it ("over quota", "exceeded") never make it a throttle of the whole address.
            if (subject == "7" || (subject == "2" && detail == "1") || (subject == "4" && detail == "5"))
            {
                return EmailFailureKind.Throttled;
            }

            return subject != "2" && ContainsAny(lower, _throttleWords) && !ContainsAny(lower, _fullMailboxWords)
                ? EmailFailureKind.Throttled
                : EmailFailureKind.Temporary;
        }

        if (severity != "5")
        {
            return null;
        }

        return subject switch
        {
            // 5.0.0 is the undefined code; hosts send it for a recipient they cannot deliver to, and for a block, which
            // only the words tell apart.
            "0" => RecipientUnlessBlocked(lower),

            // 5.1.7 and 5.1.8 are about the sender's own address or domain, not the recipient's.
            "1" when detail is "7" or "8" => EmailFailureKind.Blocked,
            "1" => EmailFailureKind.HardBounce,

            // 5.2.1 is a disabled mailbox; 5.2.2 is a full one, which may empty; 5.2.3 is an email too large for the
            // mailbox, which a smaller email would not meet.
            "2" when detail == "1" => EmailFailureKind.HardBounce,
            "2" when detail == "3" => EmailFailureKind.Rejected,
            "2" => EmailFailureKind.Temporary,

            // 5.3.x is the receiving mail system's own status, such as 5.3.4, an email too large to take.
            "3" => MessageUnlessUnknownRecipient(lower),

            // 5.4.5 is congestion; Gmail answers with it when the sender's own daily quota is spent, which is the
            // sender's limit, not the recipient's. 5.4.7 is an email that expired in the queue while the receiving
            // server kept deferring; the address may be good.
            "4" when detail == "5" => EmailFailureKind.Throttled,
            "4" when detail == "7" => EmailFailureKind.Temporary,

            // The rest of 5.4.x is routing: 5.4.4 is a domain that does not exist or takes no mail, and 5.4.1 is how
            // some hosts answer for a recipient they do not have.
            "4" => RecipientUnlessBlocked(lower),

            // 5.5.x is the protocol, and 5.6.x the content: the one email was refused. Some hosts answer 5.5.0 for a
            // recipient they do not have, which the words say.
            "5" => MessageUnlessUnknownRecipient(lower),
            "6" => MessageUnlessUnknownRecipient(lower),

            // 5.7.x is the security and policy class: a refusal of the sender.
            "7" => EmailFailureKind.Blocked,
            _ => MessageUnlessUnknownRecipient(lower),
        };
    }

    // A refusal of the recipient's address, unless the words say the sender was refused (a block list, a policy) and
    // never that the recipient is unknown.
    private static EmailFailureKind RecipientUnlessBlocked(string lower)
        => ContainsAny(lower, _blockWords) && !ContainsAny(lower, _unknownRecipientWords)
            ? EmailFailureKind.Blocked
            : EmailFailureKind.HardBounce;

    // A refusal of the one email, unless the words say the recipient is unknown.
    private static EmailFailureKind MessageUnlessUnknownRecipient(string lower)
        => ContainsAny(lower, _unknownRecipientWords)
            ? EmailFailureKind.HardBounce
            : EmailFailureKind.Rejected;

    private static bool ContainsAny(string text, string[] words)
        => words.Any(word => text.Contains(word, StringComparison.Ordinal));

    [GeneratedRegex(@"\b([245])\.(\d{1,3})\.(\d{1,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex EnhancedStatusPattern();

    [GeneratedRegex(@"\b5[05][0-9]\b", RegexOptions.CultureInvariant)]
    private static partial Regex BasicPermanentPattern();

    [GeneratedRegex(@"\b4[0-9]{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex BasicTemporaryPattern();
}
