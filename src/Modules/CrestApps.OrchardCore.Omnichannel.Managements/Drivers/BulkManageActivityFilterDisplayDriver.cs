using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
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
    private readonly LinkGenerator _linkGenerator;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BulkManageActivityFilterDisplayDriver(
        LinkGenerator linkGenerator,
        ISession session,
        IClock clock,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<BulkManageActivityFilterDisplayDriver> stringLocalizer)
    {
        _linkGenerator = linkGenerator;
        _session = session;
        _clock = clock;
        _subjectFlowSettingsService = subjectFlowSettingsService;
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
            model.Limit = filter.Limit;
            model.PhoneNumber = filter.PhoneNumber;
            model.PhoneNumberMatchType = filter.PhoneNumberMatchType;
            model.TimeZoneIds = filter.TimeZoneIds ?? [];
            model.DoNotCallFrom = filter.DoNotCallFrom?.ToString("yyyy-MM-dd");
            model.DoNotCallTo = filter.DoNotCallTo?.ToString("yyyy-MM-dd");

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

            model.PhoneNumberMatchTypes =
            [
                new(S["Begins with"], nameof(PhoneNumberMatchType.BeginsWith)),
                new(S["Ends with"], nameof(PhoneNumberMatchType.EndsWith)),
                new(S["Exact match"], nameof(PhoneNumberMatchType.Exact)),
            ];

            var subjectContentTypes = new List<SelectListItem>()
            {
                new(S["Any subject"], ""),
            };

            foreach (var contentType in await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync())
            {
                subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
            }

            model.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);

            var timeZones = new List<SelectListItem>();

            foreach (var timeZone in _clock.GetTimeZones())
            {
                timeZones.Add(new SelectListItem(timeZone.TimeZoneId, timeZone.TimeZoneId));
            }

            model.TimeZones = timeZones.OrderBy(x => x.Text);

            model.UserSearchEndpoint = _linkGenerator.GetPathByName("CrestApps.Users.Search", new { valueType = "userId" });

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
        filter.Limit = model.Limit;
        filter.PhoneNumber = model.PhoneNumber?.Trim();
        filter.PhoneNumberMatchType = model.PhoneNumberMatchType;
        filter.TimeZoneIds = model.TimeZoneIds;
        filter.DoNotCallFrom = null;
        filter.DoNotCallTo = null;

        if (!string.IsNullOrEmpty(filter.PhoneNumber) && !filter.PhoneNumber.StartsWith('+'))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PhoneNumber), S["Phone number must be in E.164 format (e.g., +17025551234 for US/Canada)."]);
        }

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

        if (!string.IsNullOrEmpty(model.DoNotCallFrom) && DateTime.TryParseExact(model.DoNotCallFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dncFrom))
        {
            filter.DoNotCallFrom = dncFrom;
        }

        if (!string.IsNullOrEmpty(model.DoNotCallTo) && DateTime.TryParseExact(model.DoNotCallTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dncTo))
        {
            filter.DoNotCallTo = dncTo;
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

        if (filter.Limit.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".Limit", filter.Limit.Value.ToString());
        }

        if (!string.IsNullOrEmpty(filter.PhoneNumber))
        {
            filter.RouteValues.TryAdd(Prefix + ".PhoneNumber", filter.PhoneNumber);
            filter.RouteValues.TryAdd(Prefix + ".PhoneNumberMatchType", filter.PhoneNumberMatchType.ToString());
        }

        if (filter.TimeZoneIds is { Length: > 0 })
        {
            for (var i = 0; i < filter.TimeZoneIds.Length; i++)
            {
                filter.RouteValues.TryAdd($"{Prefix}.TimeZoneIds[{i}]", filter.TimeZoneIds[i]);
            }
        }

        if (filter.DoNotCallFrom.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".DoNotCallFrom", filter.DoNotCallFrom.Value.ToString("yyyy-MM-dd"));
        }

        if (filter.DoNotCallTo.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".DoNotCallTo", filter.DoNotCallTo.Value.ToString("yyyy-MM-dd"));
        }

        return Edit(filter, context);
    }
}
