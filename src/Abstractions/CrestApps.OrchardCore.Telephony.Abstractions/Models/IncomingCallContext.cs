namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the contextual information shown alongside a ringing inbound call in the soft-phone
/// incoming-call modal. Modules contribute the cards through an
/// <see cref="IIncomingCallContextProvider"/>. The context is serialized to the soft-phone client.
/// </summary>
public sealed class IncomingCallContext
{
    /// <summary>
    /// Gets or sets the optional heading shown above the contributed cards.
    /// </summary>
    public string Heading { get; set; }

    /// <summary>
    /// Gets or sets the cards contributed for the incoming call, ordered for display.
    /// </summary>
    public IList<IncomingCallCard> Cards { get; set; } = [];

    /// <summary>
    /// Gets or sets additional metadata contributors can attach for the client, such as a queue name.
    /// </summary>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
