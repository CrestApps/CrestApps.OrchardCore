namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Models;

public sealed class EventGridSettings
{
    public string EventGridSasKey { get; set; }

    public string AADIssuer { get; set; }

    public string AADAudience { get; set; }
}
