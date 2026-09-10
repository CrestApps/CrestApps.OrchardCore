namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the shared (channel-neutral) channel-endpoint fields. Channel-specific fields
/// such as the provider and routing are contributed by the display drivers that target each channel.
/// </summary>
public class OmnichannelChannelEndpointViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the channel (the source key). Shown read-only; set when the endpoint is created.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; }
}
