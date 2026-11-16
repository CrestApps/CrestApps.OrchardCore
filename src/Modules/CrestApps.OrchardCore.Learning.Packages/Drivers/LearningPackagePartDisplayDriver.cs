using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// The package part in the content editor: what the last import found (the file, the title, the launched SCO and its
/// mastery score) and a link to import a package. The values are set only by an import.
/// </summary>
internal sealed class LearningPackagePartDisplayDriver : ContentPartDisplayDriver<LearningPackagePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public LearningPackagePartDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(LearningPackagePart part, BuildPartEditorContext context)
        => Initialize<LearningPackagePartViewModel>(GetEditorShapeType(context), async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.ContentItemId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;
            model.Part = part;
            model.CanImport = user is not null && await _authorizationService.AuthorizeAsync(user, LearningPackagesPermissions.ImportLearningPackages);
        });
}
