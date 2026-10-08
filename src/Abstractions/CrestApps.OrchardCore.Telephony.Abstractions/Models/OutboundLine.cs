namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a phone number a user dials out from: the caller ID presented on the calls they place.
/// </summary>
public sealed class OutboundLine
{
    /// <summary>
    /// Gets the identifier of the record that defines the line, such as a channel endpoint.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the name the line is known by, such as "Sales line".
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the phone number presented as the caller ID, in E.164 form.
    /// </summary>
    public string Number { get; init; }
}
