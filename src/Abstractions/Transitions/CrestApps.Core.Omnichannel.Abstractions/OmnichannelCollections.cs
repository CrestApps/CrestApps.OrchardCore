namespace CrestApps.Core.Omnichannel;

/// <summary>
/// The storage collection the omnichannel documents live in.
/// </summary>
/// <remarks>
/// Split out of the host's constants for the same reason the channel names were: the indexes and the schema
/// migrations that build their tables need to name the collection, and they are framework code. The value is
/// the one already written into every deployment's table names, so it is a constant rather than a setting -
/// changing it would orphan the tables rather than rename them.
/// </remarks>
public static class OmnichannelCollections
{
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = "Omnichannel";
}
