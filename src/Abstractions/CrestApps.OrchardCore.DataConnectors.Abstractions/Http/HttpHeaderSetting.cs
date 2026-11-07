namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// A header sent with every request of a connection.
/// </summary>
public sealed class HttpHeaderSetting
{
    /// <summary>
    /// Gets or sets the header's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the header's value.
    /// </summary>
    public string Value { get; set; }
}
