namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// What the dial destination policy needs to know about the attempt beyond the address itself.
/// </summary>
public sealed class DialDestinationContext
{
    /// <summary>
    /// Gets or sets the operation the address is being evaluated for. Some deployments refuse a destination for a
    /// transfer that they still permit for a direct dial.
    /// </summary>
    public DialDestinationOperation Operation { get; set; } = DialDestinationOperation.Dial;

    /// <summary>
    /// Gets or sets the trunk prefix an agent may type before an outside number, when the deployment uses one. It
    /// is stripped before the address is matched, so an emergency code dialed behind the prefix is still refused.
    /// </summary>
    public string TrunkPrefix { get; set; }
}
