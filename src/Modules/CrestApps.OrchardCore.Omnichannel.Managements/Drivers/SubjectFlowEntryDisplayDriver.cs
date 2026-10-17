using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Builds the row of a subject content type on the Subject Flows page.
/// </summary>
internal sealed class SubjectFlowEntryDisplayDriver : DisplayDriver<SubjectFlowEntry>
{
    // The permission of the Content Types feature, which this module does not reference.
    private static readonly Permission _editContentTypes = new("EditContentTypes");

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowEntryDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SubjectFlowEntryDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<IDisplayResult> DisplayAsync(SubjectFlowEntry entry, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{entry.DisplayName} {entry.ContentTypeName}".ToLowerInvariant();
        }

        return CombineAsync(
            View("SubjectFlowEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SubjectFlowEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("SubjectFlowEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Direction:5"),
            View("SubjectFlowEntry_DefaultMeta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Flow:5")
                .RenderWhen(() => Task.FromResult(!entry.HasActions)),
            View("SubjectFlowEntry_Buttons_SummaryAdmin", entry).Location("Actions:5"),
            View("SubjectFlowEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:5")
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, _editContentTypes)));
    }
}
