namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Which indexed versions of a contact or lead a lookup reads.
/// </summary>
public enum OmnichannelContactVersion
{
    /// <summary>
    /// Every version the index holds: the published one and the latest one.
    /// </summary>
    Any,

    /// <summary>
    /// The published version only.
    /// </summary>
    Published,

    /// <summary>
    /// The latest version only, published or not.
    /// </summary>
    Latest,
}
