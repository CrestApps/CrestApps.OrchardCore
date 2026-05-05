using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver for the bulk manage activity filter shape.
/// </summary>
internal sealed class BulkManageActivityFilterDisplayDriver : DisplayDriver<BulkManageActivityFilter>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BulkManageActivityFilterDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        IStringLocalizer<BulkManageActivityFilterDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        S = stringLocalizer;
    }

    protected override void BuildPrefix(BulkManageActivityFilter model, string htmlFieldPrefix)
    {
        Prefix = "Filter";
    }

    public override IDisplayResult Edit(BulkManageActivityFilter filter, BuildEditorContext context)
    {
        return Initialize<BulkManageActivityFilterViewModel>("BulkManageActivityFilterFields_Edit", async model =>
        {
            model.ContactIsPublished = filter.ContactIsPublished?.ToString();
            model.AttemptFilter = filter.AttemptFilter;
            model.SubjectContentType = filter.SubjectContentType;
            model.Channel = filter.Channel;
            model.AssignedToUserIds = filter.AssignedToUserIds ?? [];
            model.UrgencyLevel = filter.UrgencyLevel?.ToString();
            model.ScheduledFrom = filter.ScheduledFrom?.ToString("yyyy-MM-dd");
            model.ScheduledTo = filter.ScheduledTo?.ToString("yyyy-MM-dd");
            model.CreatedFrom = filter.CreatedFrom?.ToString("yyyy-MM-dd");
            model.CreatedTo = filter.CreatedTo?.ToString("yyyy-MM-dd");

            model.ContactPublishedOptions =
            [
                new(S["Any status"], ""),
                new(S["Published contacts"], "True"),
                new(S["Unpublished contacts"], "False"),
            ];

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
                new(S["No attempt"], "0"),
                new(S["2 attempts"], "2"),
                new(S["3 attempts"], "3"),
                new(S["4 attempts"], "4"),
                new(S["5 attempts"], "5"),
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

            model.UserSearchEndpoint = "~/Admin/api/crestapps/users/search?valueType=userId";

            if (filter.AssignedToUserIds is { Length: > 0 })
            {
                var selectedUsers = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(filter.AssignedToUserIds))
                    .ListAsync();

                model.SelectedAssignedUsersJson = System.Text.Json.JsonSerializer.Serialize(
                    selectedUsers.Select(u => new { value = u.UserId, text = u.UserName, selected = true }));
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(BulkManageActivityFilter filter, UpdateEditorContext context)
    {
        var model = new BulkManageActivityFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.SubjectContentType = model.SubjectContentType;
        filter.Channel = model.Channel;
        filter.AttemptFilter = model.AttemptFilter;
        filter.AssignedToUserIds = model.AssignedToUserIds;
        filter.ContactIsPublished = null;
        filter.UrgencyLevel = null;
        filter.ScheduledFrom = null;
        filter.ScheduledTo = null;
        filter.CreatedFrom = null;
        filter.CreatedTo = null;

        if (!string.IsNullOrEmpty(model.ContactIsPublished) && bool.TryParse(model.ContactIsPublished, out var isPublished))
        {
            filter.ContactIsPublished = isPublished;
        }

        if (!string.IsNullOrEmpty(model.UrgencyLevel) && Enum.TryParse<ActivityUrgencyLevel>(model.UrgencyLevel, out var urgencyLevel))
        {
            filter.UrgencyLevel = urgencyLevel;
        }

        if (!string.IsNullOrEmpty(model.ScheduledFrom) && DateTime.TryParseExact(model.ScheduledFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledFrom))
        {
            filter.ScheduledFrom = scheduledFrom;
        }

        if (!string.IsNullOrEmpty(model.ScheduledTo) && DateTime.TryParseExact(model.ScheduledTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTo))
        {
            filter.ScheduledTo = scheduledTo;
        }

        if (!string.IsNullOrEmpty(model.CreatedFrom) && DateTime.TryParseExact(model.CreatedFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdFrom))
        {
            filter.CreatedFrom = createdFrom;
        }

        if (!string.IsNullOrEmpty(model.CreatedTo) && DateTime.TryParseExact(model.CreatedTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdTo))
        {
            filter.CreatedTo = createdTo;
        }

        // Populate route values for pagination link generation.

        if (filter.ContactIsPublished.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ContactIsPublished", filter.ContactIsPublished.Value.ToString());
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

        if (filter.UrgencyLevel.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".UrgencyLevel", filter.UrgencyLevel.Value.ToString());
        }

        if (filter.ScheduledFrom.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledFrom", filter.ScheduledFrom.Value.ToString("yyyy-MM-dd"));
        }

        if (filter.ScheduledTo.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledTo", filter.ScheduledTo.Value.ToString("yyyy-MM-dd"));
        }

        if (filter.CreatedFrom.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".CreatedFrom", filter.CreatedFrom.Value.ToString("yyyy-MM-dd"));
        }

        if (filter.CreatedTo.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".CreatedTo", filter.CreatedTo.Value.ToString("yyyy-MM-dd"));
        }

        if (filter.AssignedToUserIds is { Length: > 0 })
        {
            for (var i = 0; i < filter.AssignedToUserIds.Length; i++)
            {
                filter.RouteValues.TryAdd($"{Prefix}.AssignedToUserIds[{i}]", filter.AssignedToUserIds[i]);
            }
        }

        return Edit(filter, context);
    }
}
