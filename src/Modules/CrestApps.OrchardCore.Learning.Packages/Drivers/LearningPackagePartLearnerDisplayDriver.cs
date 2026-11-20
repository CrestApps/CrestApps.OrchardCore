using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// The package panel on a learning package item in the learner player: the learner's status, score, time and attempts
/// from their runtime state, and the Launch (or Resume) button that opens the package in its sandboxed frame, or in a
/// window of its own when the package says so. Only the signed-in learner's own enrollment and runtime state are read.
/// </summary>
internal sealed class LearningPackagePartLearnerDisplayDriver : ContentPartDisplayDriver<LearningPackagePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PackagePanelBuilder _panelBuilder;

    public LearningPackagePartLearnerDisplayDriver(IHttpContextAccessor httpContextAccessor, PackagePanelBuilder panelBuilder)
    {
        _httpContextAccessor = httpContextAccessor;
        _panelBuilder = panelBuilder;
    }

    public override IDisplayResult Display(LearningPackagePart part, BuildPartDisplayContext context)
        => Initialize<PackagePanelViewModel>("LearningPackagePart_Learner", async model =>
        {
            await _panelBuilder.FillAsync(model, part, _httpContextAccessor.HttpContext?.User);
        }).Location("Detail", "Content:5");
}
