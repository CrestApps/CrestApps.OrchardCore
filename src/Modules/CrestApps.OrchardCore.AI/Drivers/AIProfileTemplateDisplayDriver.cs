using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for the generic fields shared by all template sources:
/// Title, Technical Name, Description, Category, and IsListable.
/// </summary>
internal sealed class AIProfileTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDisplayDriver"/> class.
    /// </summary>
    /// <param name="templatesCatalog">The templates catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateDisplayDriver(
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProfileTemplateDisplayDriver> stringLocalizer)
    {
        _templatesCatalog = templatesCatalog;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfileTemplate template, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfileTemplate_Fields_SummaryAdmin", template).Location("Content:1"),
            // Managing templates and managing profiles are separate permissions, so the shortcut into the
            // "New AI profile" wizard shows only to someone who may create the profile it leads to.
            View("AIProfileTemplate_CreateProfile_SummaryAdmin", template).Location("Actions:1")
            .RenderWhen(async () => string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase) &&
                await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles)),
            View("AIProfileTemplate_Buttons_SummaryAdmin", template).Location("Actions:5"),
            View("AIProfileTemplate_DefaultTags_SummaryAdmin", template).Location("Tags:5"),
            View("AIProfileTemplate_DefaultMeta_SummaryAdmin", template).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileTemplateFieldsViewModel>("AIProfileTemplateFields_Edit", model =>
        {
            model.DisplayText = template.DisplayText;
            model.Name = template.Name;
            model.Description = template.Description;
            model.Category = template.Category;
            model.IsListable = template.IsListable;
            model.IsNew = context.IsNew;
            model.IsProfileTemplate = string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase);
        }).Location(template.Source == AITemplateSources.Profile ? "Content:1%General;1" : "Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new AIProfileTemplateFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            if (string.IsNullOrEmpty(model.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _templatesCatalog.FindByNameAsync(model.Name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another template with the same name exists."]);
            }

            template.Name = model.Name;
        }

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Title is required."]);
        }

        template.DisplayText = model.DisplayText;
        template.Description = model.Description;
        template.Category = model.Category;
        template.IsListable = model.IsListable;

        return Edit(template, context);
    }
}
