using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// The package panel on a learning package item in the learner player: the learner's status, score and time from their
/// runtime state, and the Launch (or Resume) button that opens the package in its sandboxed frame. Only the signed-in
/// learner's own enrollment and runtime state are read.
/// </summary>
internal sealed class LearningPackagePartLearnerDisplayDriver : ContentPartDisplayDriver<LearningPackagePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseItemLearnerResolver _learnerResolver;
    private readonly PackageRuntimeService _runtime;

    public LearningPackagePartLearnerDisplayDriver(IHttpContextAccessor httpContextAccessor, CourseItemLearnerResolver learnerResolver, PackageRuntimeService runtime)
    {
        _httpContextAccessor = httpContextAccessor;
        _learnerResolver = learnerResolver;
        _runtime = runtime;
    }

    public override IDisplayResult Display(LearningPackagePart part, BuildPartDisplayContext context)
        => Initialize<PackagePanelViewModel>("LearningPackagePart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
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
            }
        }).Location("Detail", "Content:5");
}
