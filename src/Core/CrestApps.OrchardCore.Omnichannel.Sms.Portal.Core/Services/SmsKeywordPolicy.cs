using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Classifies an inbound message as a carrier keyword and says what to reply. A keyword is the whole message:
/// matching it as a prefix opted people out of a service they were in the middle of asking about ("stop by the
/// shop later"), so only trailing punctuation and whitespace are tolerated.
/// </summary>
public static class SmsKeywordPolicy
{
    private static readonly string[] _stopKeywords =
        ["STOP", "STOPALL", "UNSUBSCRIBE", "CANCEL", "END", "QUIT"];

    private static readonly string[] _helpKeywords = ["HELP", "INFO"];

    private static readonly string[] _startKeywords = ["START", "UNSTOP", "YES"];

    /// <summary>
    /// The confirmation sent when a contact opts out, used when the tenant has not written its own. It has to
    /// exist: a tenant that configured nothing still owes a contact who texts STOP an answer.
    /// </summary>
    public const string DefaultStopMessage = "You have been unsubscribed and will not receive further messages. Reply START to resubscribe.";

    /// <summary>
    /// The default reply to HELP.
    /// </summary>
    public const string DefaultHelpMessage = "Reply STOP to unsubscribe. Message and data rates may apply.";

    /// <summary>
    /// The default confirmation sent when a contact opts back in.
    /// </summary>
    public const string DefaultStartMessage = "You have been resubscribed and will receive messages again. Reply STOP to unsubscribe.";

    /// <summary>
    /// Classifies an inbound message body.
    /// </summary>
    /// <param name="body">The inbound message body.</param>
    /// <returns>The keyword the contact sent, or <see cref="SmsKeyword.None"/>.</returns>
    public static SmsKeyword Classify(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return SmsKeyword.None;
        }

        var normalized = body.Trim().TrimEnd('.', '!', '?', ',', ';', ':');

        if (Matches(normalized, _stopKeywords))
        {
            return SmsKeyword.Stop;
        }

        if (Matches(normalized, _helpKeywords))
        {
            return SmsKeyword.Help;
        }

        return Matches(normalized, _startKeywords)
            ? SmsKeyword.Start
            : SmsKeyword.None;
    }

    /// <summary>
    /// Returns the reply owed for a keyword, or <see langword="null"/> when none is.
    /// </summary>
    /// <param name="keyword">The keyword the contact sent.</param>
    /// <param name="settings">The tenant's configured replies.</param>
    public static string ReplyFor(SmsKeyword keyword, SmsKeywordReplySettings settings)
    {
        return keyword switch
        {
            SmsKeyword.Stop => Coalesce(settings?.StopMessage, DefaultStopMessage),
            SmsKeyword.Help => Coalesce(settings?.HelpMessage, DefaultHelpMessage),
            SmsKeyword.Start => Coalesce(settings?.StartMessage, DefaultStartMessage),
            _ => null,
        };
    }

    private static string Coalesce(string configured, string fallback)
        => string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

    private static bool Matches(string normalized, string[] keywords)
        => keywords.Any(keyword => string.Equals(normalized, keyword, StringComparison.OrdinalIgnoreCase));
}
