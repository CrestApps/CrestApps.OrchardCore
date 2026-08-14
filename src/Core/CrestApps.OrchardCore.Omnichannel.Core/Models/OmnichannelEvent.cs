namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel event.
/// </summary>
public sealed class OmnichannelEvent
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public OmnichannelMessage Message { get; set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public BinaryData Data { get; set; }

    /// <summary>
    /// Retrieves the data from json.
    /// </summary>
    public T GetDataFromJson<T>()
    {
        if (Data == null)
        {
            return default;
        }

        return Data.ToObjectFromJson<T>();
    }
}
