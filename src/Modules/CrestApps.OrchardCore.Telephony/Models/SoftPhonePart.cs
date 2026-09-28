using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Marker content part attached to the Soft Phone widget content type. It carries no data of its own; its
/// display driver renders the floating soft phone shape using the site-wide soft phone settings, so an
/// operator can place the phone on the front end through Design &gt; Widgets instead of it being auto-injected.
/// </summary>
public sealed class SoftPhonePart : ContentPart
{
}
