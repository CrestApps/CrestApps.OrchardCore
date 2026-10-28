using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The versions page of an AI profile.
/// </summary>
public class AIProfileVersionsViewModel
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the version the profile is at.
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Gets or sets the versions, rendered by the admin list.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// A row of the versions list.
/// </summary>
public class AIProfileVersionEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionEntry"/> class.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <param name="isCurrent">Whether the profile is at this version.</param>
    public AIProfileVersionEntry(AIProfileVersion version, bool isCurrent)
    {
        Version = version;
        IsCurrent = isCurrent;
    }

    /// <summary>
    /// Gets the version.
    /// </summary>
    public AIProfileVersion Version { get; }

    /// <summary>
    /// Gets whether the profile is at this version.
    /// </summary>
    public bool IsCurrent { get; }
}

/// <summary>
/// Two copies of an AI profile side by side: a version and the profile as it is now, or two versions.
/// </summary>
public class AIProfileVersionCompareViewModel
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the version on the left.
    /// </summary>
    public AIProfileVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the version on the right, or <see langword="null"/> for the profile as it is now.
    /// </summary>
    public AIProfileVersion Other { get; set; }

    /// <summary>
    /// Gets or sets the version the profile is at.
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Gets or sets the profile's recent versions, to pick what the version is compared with.
    /// </summary>
    public IReadOnlyList<AIProfileVersion> Versions { get; set; } = [];

    /// <summary>
    /// Gets or sets what each holds.
    /// </summary>
    public IReadOnlyList<AIProfileVersionCompareRow> Rows { get; set; } = [];
}
