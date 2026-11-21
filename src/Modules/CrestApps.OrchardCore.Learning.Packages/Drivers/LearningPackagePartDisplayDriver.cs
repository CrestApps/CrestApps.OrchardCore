using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// The package part in the content editor: what the last import found (the file, the title, the launched SCO and its
/// mastery score) and a link to import a package, which are set only by an import, and how learners run it: a window of
/// its own, a limit on attempts and the grading method.
/// </summary>
internal sealed class LearningPackagePartDisplayDriver : ContentPartDisplayDriver<LearningPackagePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public LearningPackagePartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<LearningPackagePartDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(LearningPackagePart part, BuildPartEditorContext context)
        => Initialize<LearningPackagePartViewModel>(GetEditorShapeType(context), async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.ContentItemId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;
            model.Part = part;
            model.CanImport = user is not null && await _authorizationService.AuthorizeAsync(user, LearningPackagesPermissions.ImportLearningPackages);
            model.OpenInNewWindow = part.OpenInNewWindow;
            model.MaxAttempts = part.MaxAttempts;
            model.GradingMethod = part.GradingMethod?.ToString();
        });

    public override async Task<IDisplayResult> UpdateAsync(LearningPackagePart part, UpdatePartEditorContext context)
    {
        var model = new LearningPackagePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxAttempts is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxAttempts), S["A learner has at least one attempt, or leave it empty for no limit."]);
        }

        GradingMethod? method = null;

        if (!string.IsNullOrEmpty(model.GradingMethod))
        {
            if (Enum.TryParse<GradingMethod>(model.GradingMethod, out var parsed) && Enum.IsDefined(parsed))
            {
                method = parsed;
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.GradingMethod), S["Choose which attempts set the score."]);
            }
        }

        part.OpenInNewWindow = model.OpenInNewWindow;
        part.MaxAttempts = model.MaxAttempts is > 0 ? model.MaxAttempts : null;
        part.GradingMethod = method;

        return Edit(part, context);
    }
}
