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
}

/// <summary>
/// Reads a failed send's answer (the SMTP code, the enhanced status code, and the words around them) and decides what
/// it means. The enhanced code decides when there is one: 5.1.x and 5.2.1 are a dead address, 5.7.x a block, 4.7.x
/// and the rate words a throttle. A bare 550 is only a dead address when the words say so, because mail servers also
/// answer 550 to blocks.
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

        if (ContainsAny(lower, _throttleWords))
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
    {
        var match = EnhancedStatusPattern().Match(status ?? string.Empty);

        return match.Success
            ? ClassifyEnhanced(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, status)
            : null;
    }

    private static EmailFailureKind? ClassifyEnhanced(string severity, string subject, string detail, string text)
    {
        var lower = text?.ToLowerInvariant() ?? string.Empty;

        if (severity == "4")
        {
            // 4.7.x is the security and policy class, which is how mail servers say "slow down"; 4.2.1 and 4.4.5 are
            // mailbox and system congestion.
            return subject == "7" || (subject == "2" && detail == "1") || (subject == "4" && detail == "5") || ContainsAny(lower, _throttleWords)
                ? EmailFailureKind.Throttled
                : EmailFailureKind.Temporary;
        }

        if (severity != "5")
        {
            return null;
        }

        return subject switch
        {
            // 5.1.7 and 5.1.8 are about the sender's own address or domain, not the recipient's.
            "1" when detail is "7" or "8" => EmailFailureKind.Blocked,
            "1" => EmailFailureKind.HardBounce,

            // 5.2.1 is a disabled mailbox; 5.2.2 is a full one, which may empty.
            "2" when detail == "1" => EmailFailureKind.HardBounce,
            "2" => EmailFailureKind.Temporary,
            "7" => EmailFailureKind.Blocked,

            // 5.4.1 and 5.5.0 are how some hosts answer for a recipient they do not have.
            "4" when detail == "1" && !ContainsAny(lower, _blockWords) => EmailFailureKind.HardBounce,
            "5" when detail == "0" && ContainsAny(lower, _unknownRecipientWords) => EmailFailureKind.HardBounce,
            _ => ContainsAny(lower, _unknownRecipientWords) ? EmailFailureKind.HardBounce : EmailFailureKind.Blocked,
        };
    }

    private static bool ContainsAny(string text, string[] words)
        => words.Any(word => text.Contains(word, StringComparison.Ordinal));

    [GeneratedRegex(@"\b([245])\.(\d{1,3})\.(\d{1,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex EnhancedStatusPattern();

    [GeneratedRegex(@"\b5[05][0-9]\b", RegexOptions.CultureInvariant)]
    private static partial Regex BasicPermanentPattern();
}
