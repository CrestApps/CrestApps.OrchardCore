using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver that provides the bulk activity actions panel shape.
/// </summary>
internal sealed class BulkManageActivityActionsDisplayDriver : DisplayDriver<BulkManageOmnichannelActivityContainer>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityActionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BulkManageActivityActionsDisplayDriver(
        LinkGenerator linkGenerator,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<BulkManageActivityActionsDisplayDriver> stringLocalizer)
    {
        _linkGenerator = linkGenerator;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(BulkManageOmnichannelActivityContainer model, BuildDisplayContext context)
    {
        return Initialize<BulkActivityActionsViewModel>("BulkActivityActions", async vm =>
        {
            vm.UrgencyLevels =
            [
                new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
                new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
                new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
                new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
                new(S["High"], nameof(ActivityUrgencyLevel.High)),
                new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
            ];

            var subjectContentTypes = new List<SelectListItem>();

            foreach (var contentType in await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync())
            {
                subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
            }

            vm.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);
            vm.UserSearchEndpoint = _linkGenerator.GetPathByName("CrestApps.Users.Search", new { valueType = "userId" });
            vm.TotalCount = model.TotalCount;
        }).Location("Content:5");
    }
}
