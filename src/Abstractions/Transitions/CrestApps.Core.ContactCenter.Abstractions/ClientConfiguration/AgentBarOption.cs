namespace CrestApps.Core.ContactCenter.ClientConfiguration;

/// <summary>
/// One pickable option offered by the agent bar, such as a disposition or a reason code.
/// </summary>
/// <remarks>
/// Deliberately only an identifier and a label. It is serialized straight into the page, so any
/// property added here is a property the browser starts receiving.
/// </remarks>
public sealed class AgentBarOption
{
    /// <summary>
    /// Gets or sets the identifier sent back when the option is chosen.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the label shown to the agent.
    /// </summary>
    public string Name { get; set; }
}
