using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Packages.ViewModels;

/// <summary>
/// The package part in the content editor: what the last import found. Nothing in it is edited by hand.
/// </summary>
public class LearningPackagePartViewModel
{
    /// <summary>Gets or sets the content item id; empty for an item that is not saved yet.</summary>
    [BindNever]
    public string ContentItemId { get; set; }

    /// <summary>Gets or sets whether the item is new.</summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>Gets or sets whether the person may import a package.</summary>
    [BindNever]
    public bool CanImport { get; set; }

    /// <summary>Gets or sets the part.</summary>
    [BindNever]
    public LearningPackagePart Part { get; set; }
}

/// <summary>
/// The upload page of a package.
/// </summary>
public class PackageUploadViewModel
{
    /// <summary>Gets or sets the content item id.</summary>
    public string ContentItemId { get; set; }

    /// <summary>Gets or sets the title of the item.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the part as it is now.</summary>
    public LearningPackagePart Part { get; set; }

    /// <summary>Gets or sets the largest upload, in megabytes.</summary>
    public long MaxUploadMb { get; set; }
}

/// <summary>
/// The package panel of the learner player.
/// </summary>
public class PackagePanelViewModel
{
    /// <summary>Gets or sets the package item id.</summary>
    public string PackageItemId { get; set; }

    /// <summary>Gets or sets the part.</summary>
    public LearningPackagePart Part { get; set; }

    /// <summary>Gets or sets whether the person is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets whether the person has an enrollment in the course.</summary>
    public bool Enrolled { get; set; }

    /// <summary>Gets or sets whether the person can launch the package now (an active enrollment, or a completed one to review).</summary>
    public bool CanLaunch { get; set; }

    /// <summary>Gets or sets whether a launch is a review of a completed enrollment.</summary>
    public bool Review { get; set; }

    /// <summary>Gets or sets the learner's runtime state, or <see langword="null"/> before the first launch.</summary>
    public PackageRuntimeState State { get; set; }

    /// <summary>Gets or sets the score the package reported, as a percentage.</summary>
    public decimal? Score { get; set; }

    /// <summary>Gets or sets what the panel shows of the runtime state, for either standard.</summary>
    public PackageStateSummary Summary { get; set; } = PackageStateSummary.Of(null);

    /// <summary>Gets whether the next launch resumes a suspended session.</summary>
    public bool Resumes => Summary?.Resumes == true;
}
