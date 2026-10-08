namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.ViewModels;

/// <summary>
/// Edits the Send Text Message workflow task.
/// </summary>
public class SendTextMessageTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression of the number to send from.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression of the customer's number.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template of the message.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the optional Liquid expression of the agent's user name.
    /// </summary>
    public string UserName { get; set; }
}
