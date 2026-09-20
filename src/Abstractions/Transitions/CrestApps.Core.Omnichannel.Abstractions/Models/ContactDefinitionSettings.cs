namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// What a tenant has decided about one kind of contact: which preferences it tracks, and whether a
/// local time zone is required.
/// </summary>
/// <remarks>
/// The same five decisions the content-type editor has always offered. They are here so a service can
/// read them without knowing that a host stores them on a content-type part.
/// </remarks>
public sealed class ContactDefinitionSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether a time zone must be provided.
    /// </summary>
    public bool RequireTimeZone { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the contact time zone is automatically detected from the
    /// contact's phone number when one was not explicitly selected.
    /// </summary>
    public bool AutoDetectTimeZone { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the "do not call" preference is available.
    /// </summary>
    public bool UseDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the "do not SMS" preference is available.
    /// </summary>
    public bool UseDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the "do not email" preference is available.
    /// </summary>
    public bool UseDoNotEmail { get; set; }
}
