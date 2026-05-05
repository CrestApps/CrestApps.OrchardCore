using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver that provides the bulk activity actions panel shape.
/// </summary>
internal sealed class BulkManageActivityActionsDisplayDriver : DisplayDriver<BulkManageOmnichannelActivityContainer>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityActionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BulkManageActivityActionsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<BulkManageActivityActionsDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
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

            foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                if (contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
                {
                    subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
                }
            }

            vm.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);
            vm.UserSearchEndpoint = "~/Admin/api/crestapps/users/search?valueType=userId";
            vm.TotalCount = model.TotalCount;
        }).Location("Content:5");
    }
}
