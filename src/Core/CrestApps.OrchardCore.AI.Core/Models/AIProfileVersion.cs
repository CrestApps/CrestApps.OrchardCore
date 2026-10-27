using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// A saved version of an AI profile: the whole profile as it was saved, numbered from 1 for each profile.
/// </summary>
public sealed class AIProfileVersion
{
    /// <summary>
    /// Gets or sets the unique identifier of this version.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the profile this is a version of.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version number, from 1.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets when the version was saved.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets who saved it.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets a note on the version, such as the version it was restored from.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the profile as it was saved.
    /// </summary>
    public AIProfile Snapshot { get; set; }
}

/// <summary>
/// The version an AI profile is at, kept on the profile, and on each conversation it answers so reports can compare
/// versions.
/// </summary>
public sealed class AIProfileVersionMetadata
{
    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets a note for the version the next save makes. Cleared once that version is kept.
    /// </summary>
    public string PendingNote { get; set; }
}
