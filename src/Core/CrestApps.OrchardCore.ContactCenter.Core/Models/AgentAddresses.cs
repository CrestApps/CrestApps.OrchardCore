using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The numbers an agent calls and texts from: the numbers whose lines name them, or the tenant's default numbers when
/// they have none of their own.
/// </summary>
public sealed class AgentAddresses
{
    /// <summary>
    /// Gets the phone number the agent calls from, or <see langword="null"/> when there is none.
    /// </summary>
    public OmnichannelChannelEndpoint PhoneAddress { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="PhoneAddress"/> is the default phone number rather than the agent's own.
    /// </summary>
    public bool IsDefaultPhone { get; init; }

    /// <summary>
    /// Gets the number the agent texts from, or <see langword="null"/> when there is none.
    /// </summary>
    public OmnichannelChannelEndpoint SmsAddress { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="SmsAddress"/> is the default SMS number rather than the agent's own.
    /// </summary>
    public bool IsDefaultSms { get; init; }
}
