using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class ListOmnichannelActivityFilterDisplayDriver : DisplayDriver<ListOmnichannelActivityFilter>
{
    private readonly IClock _clock;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListOmnichannelActivityFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ListOmnichannelActivityFilterDisplayDriver(
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IClock clock,
        IStringLocalizer<ListOmnichannelActivityFilterDisplayDriver> stringLocalizer)
    {
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _clock = clock;
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
            model.TimeZoneId = NormalizeTimeZoneId(filter.TimeZoneId);
            model.ScheduledFrom = filter.ScheduledFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            model.ScheduledTo = filter.ScheduledTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

            model.TimeZones = await GetTimeZoneOptionsAsync(S["Any time zone"], model.TimeZoneId);

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

            foreach (var contentType in await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync())
            {
                subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
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
        filter.TimeZoneId = NormalizeTimeZoneId(model.TimeZoneId);
        filter.AttemptFilter = model.AttemptFilter;
        filter.ScheduledFrom = null;
        filter.ScheduledTo = null;

        if (!string.IsNullOrEmpty(model.ScheduledFrom) && DateTime.TryParseExact(model.ScheduledFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledFrom))
        {
            filter.ScheduledFrom = scheduledFrom;
        }

        if (!string.IsNullOrEmpty(model.ScheduledTo) && DateTime.TryParseExact(model.ScheduledTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTo))
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

        if (!string.IsNullOrEmpty(filter.TimeZoneId))
        {
            filter.RouteValues.TryAdd(Prefix + ".TimeZoneId", filter.TimeZoneId);
        }

        if (!string.IsNullOrEmpty(filter.AttemptFilter))
        {
            filter.RouteValues.TryAdd(Prefix + ".AttemptFilter", filter.AttemptFilter);
        }

        if (filter.ScheduledFrom.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledFrom", filter.ScheduledFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.ScheduledTo.HasValue)
        {
            filter.RouteValues.TryAdd(Prefix + ".ScheduledTo", filter.ScheduledTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return Edit(filter, context);
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id;
    }

    private async Task<IEnumerable<SelectListItem>> GetTimeZoneOptionsAsync(LocalizedString emptyOptionText, string selectedTimeZoneId)
    {
        var selectedIds = selectedTimeZoneId is null
            ? []
            : new[] { selectedTimeZoneId };
        var options = new List<SelectListItem>
        {
            new()
            {
                Text = emptyOptionText.Value,
                Value = string.Empty,
                Selected = string.IsNullOrEmpty(selectedTimeZoneId),
            },
        };

        options.AddRange(await GetMappedTimeZoneOptionsAsync(selectedIds));

        return options;
    }

    private async Task<IEnumerable<SelectListItem>> GetMappedTimeZoneOptionsAsync(IEnumerable<string> selectedTimeZoneIds)
    {
        var selectedIds = selectedTimeZoneIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var options = (await _timeZoneSelectListProvider.GetTimeZoneSelectListAsync())
            .Select(x => new SelectListItem(x.Value, x.Key))
            .ToList();

        foreach (var selectedTimeZoneId in selectedIds)
        {
            if (options.Any(x => string.Equals(x.Value, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new SelectListItem(selectedTimeZoneId, selectedTimeZoneId));
        }

        foreach (var option in options)
        {
            option.Selected = selectedIds.Contains(option.Value);
        }

        return options
            .OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
