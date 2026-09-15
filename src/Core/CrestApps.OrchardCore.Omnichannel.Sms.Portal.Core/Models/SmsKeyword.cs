namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// The carrier keywords a contact can text. These are not a product feature: a contact who texts STOP is
/// entitled to a confirmation and to hear nothing further, and one who texts HELP is entitled to be told who is
/// texting them.
/// </summary>
public enum SmsKeyword
{
    /// <summary>
    /// An ordinary message.
    /// </summary>
    None,

    /// <summary>
    /// The contact asked to stop receiving messages.
    /// </summary>
    Stop,

    /// <summary>
    /// The contact asked who is texting them and how to get support.
    /// </summary>
    Help,

    /// <summary>
    /// The contact asked to start receiving messages again after opting out.
    /// </summary>
    Start,
}
