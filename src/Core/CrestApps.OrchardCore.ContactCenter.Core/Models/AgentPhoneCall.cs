using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A phone call an agent is on that is no Contact Center interaction -- a number they dialed from the keypad, or an
/// extension call at either end -- as the live dashboard shows it.
/// </summary>
public sealed class AgentPhoneCall
{
    /// <summary>
    /// Gets or sets the agent's user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the call, as the soft phone's call history knows it: the leg of the agent who placed it.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the voice provider of the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is an internal extension call.
    /// </summary>
    public bool IsExtension { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent is the colleague an extension call rang, not its caller.
    /// </summary>
    public bool IsCallee { get; set; }

    /// <summary>
    /// Gets or sets which way the call goes, for the agent.
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets who the agent is talking to: the number dialed, or the colleague.
    /// </summary>
    public string Party { get; set; }

    /// <summary>
    /// Gets or sets when the call started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets the key the dashboard names the call by (see <see cref="Services.PhoneCallKey"/>).
    /// </summary>
    public string Key
        => Services.PhoneCallKey.Create(UserId, CallId);
}
