namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// An event that webhook subscriptions can choose: its name, what the merchant sees, the group it is listed under, and
/// an example of what its payload's <c>data</c> holds.
/// </summary>
public sealed class WebhookEventDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEventDescriptor"/> class.
    /// </summary>
    /// <param name="name">The event's name, such as <c>order.paid</c>.</param>
    /// <param name="displayName">What an administrator sees when choosing events.</param>
    /// <param name="group">What it is about, to group events by, such as Orders.</param>
    /// <param name="samplePayload">An example of the event's <c>data</c>, or <see langword="null"/>.</param>
    public WebhookEventDescriptor(string name, string displayName, string group, object samplePayload = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName;
        Group = group ?? string.Empty;
        SamplePayload = samplePayload;
    }

    /// <summary>
    /// Gets the event's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets what an administrator sees when choosing events.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the group the event is listed under.
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// Gets an example of the event's <c>data</c>, or <see langword="null"/>.
    /// </summary>
    public object SamplePayload { get; }
}
