namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the extension context used by modules that contribute shapes to the soft phone widget.
/// </summary>
public sealed class SoftPhoneWidgetExtensionContext
{
    /// <summary>
    /// Gets the shapes contributed to the soft phone widget.
    /// </summary>
    public IList<object> Shapes { get; } = [];
}
