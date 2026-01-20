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

internal sealed class ListOmnichannelActivityFilterDisplayDriver : DisplayDriver<ListOmnichannelActivityFilter>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public ListOmnichannelActivityFilterDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ListOmnichannelActivityFilterDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ListOmnichannelActivityFilter filter, BuildEditorContext context)
    {
        return Initialize<ListOmnichannelActivityFilterViewModel>("ListOmnichannelActivityFilter_Edit", async model =>
        {
            model.UrgencyLevel = filter.UrgencyLevel;
            model.SubjectContentType = filter.SubjectContentType;
            model.AttemptFrom = filter.AttemptFrom;
            model.AttemptTo = filter.AttemptTo;
            model.Channel = filter.Channel;

            model.UrgencyLevels =
            [
                new(S["Any urgency level"], ""),
                new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
                new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
                new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
                new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
                new(S["High"], nameof(ActivityUrgencyLevel.High)),
                new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
            ];

            model.Channels =
            [
                new(S["Any channel"], ""),
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
                new(S["Email"], OmnichannelConstants.Channels.Email),
            ];

            var subjectContentTypes = new List<SelectListItem>()
            {
                new(S["Any subject"], ""),
            };

            foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                if (contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
                {
                    subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
                }
            }

            model.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ListOmnichannelActivityFilter filter, UpdateEditorContext context)
    {
        var model = new ListOmnichannelActivityFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.SubjectContentType))
        {
            filter.SubjectContentType = model.SubjectContentType;
        }

        if (model.UrgencyLevel.HasValue)
        {
            filter.UrgencyLevel = model.UrgencyLevel.Value;
        }

        if (!string.IsNullOrEmpty(model.Channel))
        {
            filter.Channel = model.Channel;
        }

        filter.AttemptFrom = model.AttemptFrom;
        filter.AttemptTo = model.AttemptTo;

        return Edit(filter, context);
    }
}
