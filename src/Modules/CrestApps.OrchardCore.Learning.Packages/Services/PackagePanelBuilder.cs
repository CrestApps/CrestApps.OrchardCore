using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// Builds the package panel of the learner player and of the window a package opens in: the learner's status, score,
/// time and attempts from their own runtime state. Only the signed-in learner's own enrollment and state are read.
/// </summary>
public sealed class PackagePanelBuilder
{
    private readonly CourseItemLearnerResolver _learnerResolver;
    private readonly PackageRuntimeService _runtime;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagePanelBuilder"/> class.
    /// </summary>
    /// <param name="learnerResolver">Finds the learner's enrollment for an item.</param>
    /// <param name="runtime">The package runtime.</param>
    public PackagePanelBuilder(CourseItemLearnerResolver learnerResolver, PackageRuntimeService runtime)
    {
        _learnerResolver = learnerResolver;
        _runtime = runtime;
    }

    /// <summary>
    /// Fills a panel for a package item.
    /// </summary>
    /// <param name="model">The panel.</param>
    /// <param name="part">The package.</param>
    /// <param name="user">The person viewing.</param>
    /// <returns>A task.</returns>
    public async Task FillAsync(PackagePanelViewModel model, LearningPackagePart part, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(part);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var (_, enrollment) = await _learnerResolver.ResolveAsync(userId, part.ContentItem);

        model.PackageItemId = part.ContentItem.ContentItemId;
        model.Part = part;
        model.SignedIn = !string.IsNullOrEmpty(userId);
        model.Enrolled = enrollment is not null;
        model.Review = enrollment?.Status == EnrollmentStatus.Completed;
        model.CanLaunch = !string.IsNullOrEmpty(part.PackageFileId) && enrollment?.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed;

        if (enrollment is not null && string.Equals(enrollment.UserId, userId, StringComparison.Ordinal))
        {
            model.State = await _runtime.GetStateAsync(enrollment.ItemId, part.ContentItem.ContentItemId);
            model.Summary = PackageStateSummary.Of(model.State);
            model.Score = model.Summary.Score;
        }

        if (part.MaxAttempts is int max && max > 0)
        {
            model.AttemptsAllowed = max;
            model.AttemptsUsed = Math.Min(model.State?.Attempts ?? 0, max);
            model.AttemptsUsedUp = model.CanLaunch && !model.Review && PackageAttempts.UsedUp(part, model.State) &&
                PackageAttempts.StartsNewAttempt(model.State, PackagesConstants.Standards.IsScorm2004(part.Standard));
        }
    }
}
