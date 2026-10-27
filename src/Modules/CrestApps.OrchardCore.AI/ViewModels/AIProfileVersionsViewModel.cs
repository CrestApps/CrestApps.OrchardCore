using CrestApps.Core.AI.Models;
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
/// A version of an AI profile beside the profile as it is now.
/// </summary>
public class AIProfileVersionCompareViewModel
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public AIProfileVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the version the profile is at.
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Gets or sets what each holds.
    /// </summary>
    public IReadOnlyList<AIProfileVersionCompareRow> Rows { get; set; } = [];
}

/// <summary>
/// One setting of a version beside the same setting now.
/// </summary>
/// <param name="Label">The setting.</param>
/// <param name="Then">The version's value.</param>
/// <param name="Now">The value now.</param>
/// <param name="Multiline">Whether the values are long text.</param>
/// <param name="IsCode">Whether the values are JSON.</param>
public sealed record AIProfileVersionCompareRow(string Label, string Then, string Now, bool Multiline = false, bool IsCode = false)
{
    /// <summary>
    /// Gets whether the version and now differ.
    /// </summary>
    public bool Changed => !string.Equals(Then ?? string.Empty, Now ?? string.Empty, StringComparison.Ordinal);
}
