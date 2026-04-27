namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the view model for set contact communication preference activity task.
/// </summary>
public class SetContactCommunicationPreferenceActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the set do not call.
    /// </summary>
    public bool? SetDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the set do not sms.
    /// </summary>
    public bool? SetDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets the set do not email.
    /// </summary>
    public bool? SetDoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets the set do not chat.
    /// </summary>
    public bool? SetDoNotChat { get; set; }
}
