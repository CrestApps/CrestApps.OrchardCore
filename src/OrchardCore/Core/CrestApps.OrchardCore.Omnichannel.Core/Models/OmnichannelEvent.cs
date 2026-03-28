namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelEvent
{
    public string Id { get; set; }

    public string EventType { get; set; }

    public string Subject { get; set; }

    public OmnichannelMessage Message { get; set; }

    public BinaryData Data { get; set; }

    public T GetDataFromJson<T>()
    {
        if (Data == null)
        {
            return default;
        }

        return Data.ToObjectFromJson<T>();
    }
}
