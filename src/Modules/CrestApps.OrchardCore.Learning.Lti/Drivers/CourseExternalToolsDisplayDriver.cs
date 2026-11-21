using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Lti.Drivers;

/// <summary>
/// A card on the course editor that lets the course staff pick content in a registered tool that offers Deep Linking;
/// the picked items become External tool items of the course.
/// </summary>
internal sealed class CourseExternalToolsDisplayDriver : ContentPartDisplayDriver<CoursePart>
{
    private readonly IExternalToolStore _tools;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CourseExternalToolsDisplayDriver(
        IExternalToolStore tools,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _tools = tools;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(CoursePart part, BuildPartEditorContext context)
    {
        if (context.IsNew)
        {
            return null;
        }

        return Initialize<CourseExternalToolsViewModel>("CoursePart_ExternalTools", async model =>
        {
            model.CourseId = part.ContentItem.ContentItemId;
            model.Tools = (await _tools.ListAllAsync())
                .Where(tool => tool.Enabled && !string.IsNullOrWhiteSpace(tool.DeepLinkingUrl))
                .Select(tool => new SelectListItem(tool.Name, tool.ItemId))
                .ToList();
        })
        .Location("Content:30")
        .RenderWhen(async () =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            return user is not null && await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses, part.ContentItem);
        });
    }
}
