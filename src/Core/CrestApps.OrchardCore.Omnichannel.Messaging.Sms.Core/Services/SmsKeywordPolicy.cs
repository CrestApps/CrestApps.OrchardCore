using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Classifies an inbound message as a carrier keyword and says what to reply. The keywords and the matching rule are
/// the shared policy in <see cref="OmnichannelSmsComplianceHelper"/>, which the automated conversation uses too: a
/// keyword is the whole message, so only surrounding whitespace and trailing punctuation are tolerated.
/// </summary>
public static class SmsKeywordPolicy
{
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
    /// Classifies an inbound message body with the shared keyword policy.
    /// </summary>
    /// <param name="body">The inbound message body.</param>
    /// <returns>The keyword the contact sent, or <see cref="SmsKeyword.None"/>.</returns>
    public static SmsKeyword Classify(string body)
    {
        if (OmnichannelSmsComplianceHelper.IsOptOutRequest(body))
        {
            return SmsKeyword.Stop;
        }

        if (OmnichannelSmsComplianceHelper.IsHelpRequest(body))
        {
            return SmsKeyword.Help;
        }

        return OmnichannelSmsComplianceHelper.IsOptInRequest(body)
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
}
