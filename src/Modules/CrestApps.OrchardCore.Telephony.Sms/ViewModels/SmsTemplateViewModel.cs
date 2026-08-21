namespace CrestApps.OrchardCore.Telephony.Sms.ViewModels;

/// <summary>
/// The edit view model for an <c>SmsTemplate</c>.
/// </summary>
public class SmsTemplateViewModel
{
    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the template body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the template is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
