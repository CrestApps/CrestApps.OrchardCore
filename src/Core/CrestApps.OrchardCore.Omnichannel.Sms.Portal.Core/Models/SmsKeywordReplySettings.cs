namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// The replies sent for the carrier keywords. Each has a shipped default, because a tenant that has not written
/// one still has to answer STOP and HELP; the settings exist so an operator can say it in their own words and
/// name their own support channel.
/// </summary>
public sealed class SmsKeywordReplySettings
{
    /// <summary>
    /// Gets or sets the confirmation sent when a contact opts out. This is the one message a contact who has
    /// just texted STOP may still receive.
    /// </summary>
    public string StopMessage { get; set; }

    /// <summary>
    /// Gets or sets the reply sent when a contact texts HELP.
    /// </summary>
    public string HelpMessage { get; set; }

    /// <summary>
    /// Gets or sets the confirmation sent when a contact opts back in.
    /// </summary>
    public string StartMessage { get; set; }
}
