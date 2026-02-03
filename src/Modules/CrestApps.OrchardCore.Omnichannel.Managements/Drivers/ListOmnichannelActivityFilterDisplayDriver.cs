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

    protected override void BuildPrefix(ListOmnichannelActivityFilter model, string htmlFieldPrefix)
    {
        Prefix = "o";
    }

    public override IDisplayResult Edit(ListOmnichannelActivityFilter filter, BuildEditorContext context)
    {
        return Initialize<ListOmnichannelActivityFilterViewModel>("ListOmnichannelActivityFilterFields_Edit", async model =>
        {
            model.UrgencyLevel = filter.UrgencyLevel;
            model.SubjectContentType = filter.SubjectContentType;
            model.AttemptFilter = filter.AttemptFilter;
            model.Channel = filter.Channel;
            model.ScheduledFrom = filter.ScheduledFrom?.ToShortDateString();
            model.ScheduledTo = filter.ScheduledTo?.ToShortDateString();

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

            model.AttemptFilters =
            [
                new(S["Any attempt"], ""),
                new(S["No attempts"], "0"),
                new(S["1 attempt"], "1"),
                new(S["2 attempts"], "2"),
                new(S["3 attempts"], "3"),
                new(S["4 attempts"], "4"),
                new(S["5 attempts"], "5"),
                new(S["1+ attempts"], "1+"),
                new(S["2+ attempts"], "2+"),
                new(S["3+ attempts"], "3+"),
                new(S["4+ attempts"], "4+"),
                new(S["5+ attempts"], "5+"),
                new(S["2- attempts"], "2-"),
                new(S["3- attempts"], "3-"),
                new(S["4- attempts"], "4-"),
                new(S["5- attempts"], "5-"),
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

        filter.SubjectContentType = model.SubjectContentType;
        filter.UrgencyLevel = model.UrgencyLevel;
        filter.Channel = model.Channel;
        filter.AttemptFilter = model.AttemptFilter;
        filter.ScheduledFrom = null;
        filter.ScheduledTo = null;

        if (!string.IsNullOrEmpty(model.ScheduledFrom) && DateTime.TryParse(model.ScheduledFrom, out var scheduledFrom))
        {
            filter.ScheduledFrom = scheduledFrom;
        }

        if (!string.IsNullOrEmpty(model.ScheduledTo) && DateTime.TryParse(model.ScheduledTo, out var scheduledTo))
        {
            filter.ScheduledTo = scheduledTo;
        }

        // Populate route values so other modules can extend filtering and pagination preserves filter state.
        if (filter.UrgencyLevel.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".UrgencyLevel", filter.UrgencyLevel.Value.ToString());
        }

        if (!string.IsNullOrEmpty(filter.SubjectContentType))
        {
            filter.RouteValues.TryAdd(Prefix + ".SubjectContentType", filter.SubjectContentType);
        }

        if (!string.IsNullOrEmpty(filter.Channel))
        {
            filter.RouteValues.TryAdd(Prefix + ".Channel", filter.Channel);
        }

        if (!string.IsNullOrEmpty(filter.AttemptFilter))
        {
            filter.RouteValues.TryAdd(Prefix + ".AttemptFilter", filter.AttemptFilter);
        }

        if (filter.ScheduledFrom.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledFrom", filter.ScheduledFrom.Value.ToShortDateString());
        }

        if (filter.ScheduledTo.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledTo", filter.ScheduledTo.Value.ToShortDateString());
        }

        return Edit(filter, context);
    }
}
