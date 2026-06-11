using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactPartDisplayDriver : ContentPartDisplayDriver<OmnichannelContactPart>
{
    private readonly IClock _clock;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactPartDisplayDriver(
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IClock clock,
        IStringLocalizer<OmnichannelContactPartDisplayDriver> stringLocalizer)
    {
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelContactPart part, BuildPartEditorContext context)
    {
        return Initialize<OmnichannelContactPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<OmnichannelContactPartSettings>();

            model.RequireTimeZone = settings.RequireTimeZone;
            model.UseDoNotCall = settings.UseDoNotCall;
            model.UseDoNotSms = settings.UseDoNotSms;
            model.UseDoNotChat = settings.UseDoNotChat;
            model.UseDoNotEmail = settings.UseDoNotEmail;
            model.TimeZoneId = NormalizeTimeZoneId(part.TimeZoneId);
            model.AvailableTimeZones = await GetTimeZoneOptionsAsync(model.TimeZoneId);
            model.DoNotCall = part.DoNotCall;
            model.DoNotCallUtc = part.DoNotCallUtc;
            model.DoNotSms = part.DoNotSms;
            model.DoNotSmsUtc = part.DoNotSmsUtc;
            model.DoNotChat = part.DoNotChat;
            model.DoNotChatUtc = part.DoNotChatUtc;
            model.DoNotEmail = part.DoNotEmail;
            model.DoNotEmailUtc = part.DoNotEmailUtc;
        }).Location("Parts:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelContactPart part, UpdatePartEditorContext context)
    {
        var model = new OmnichannelContactPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<OmnichannelContactPartSettings>();

        part.TimeZoneId = NormalizeTimeZoneId(model.TimeZoneId);

        if (settings.RequireTimeZone && string.IsNullOrEmpty(part.TimeZoneId))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TimeZoneId), S["The contact time zone is required."]);
        }

        var utcNow = _clock.UtcNow;

        if (settings.UseDoNotCall)
        {
            part.SetDoNotCall(model.DoNotCall, utcNow);
        }

        if (settings.UseDoNotSms)
        {
            part.SetDoNotSms(model.DoNotSms, utcNow);
        }

        if (settings.UseDoNotChat)
        {
            part.SetDoNotChat(model.DoNotChat, utcNow);
        }

        if (settings.UseDoNotEmail)
        {
            part.SetDoNotEmail(model.DoNotEmail, utcNow);
        }

        return Edit(part, context);
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id;
    }

    private async Task<IEnumerable<SelectListItem>> GetTimeZoneOptionsAsync(string selectedTimeZoneId)
    {
        var selectedIds = selectedTimeZoneId is null
            ? []
            : new[] { selectedTimeZoneId };
        var options = (await _timeZoneSelectListProvider.GetTimeZoneSelectListAsync())
            .Select(x => new SelectListItem(x.Value, x.Key))
            .ToList();
        var normalizedSelectedIds = selectedIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedTimeZoneIdValue in normalizedSelectedIds)
        {
            if (options.Any(x => string.Equals(x.Value, selectedTimeZoneIdValue, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new SelectListItem(selectedTimeZoneIdValue, selectedTimeZoneIdValue));
        }

        foreach (var option in options)
        {
            option.Selected = normalizedSelectedIds.Contains(option.Value);
        }

        return options
            .OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
