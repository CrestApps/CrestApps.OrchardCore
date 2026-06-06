using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactPartDisplayDriver : ContentPartDisplayDriver<OmnichannelContactPart>
{
    private readonly IClock _clock;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactPartDisplayDriver(
        IClock clock,
        IStringLocalizer<OmnichannelContactPartDisplayDriver> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelContactPart part, BuildPartEditorContext context)
    {
        return Initialize<OmnichannelContactPartViewModel>(GetEditorShapeType(context), model =>
        {
            var settings = context.TypePartDefinition.GetSettings<OmnichannelContactPartSettings>();

            model.RequireTimeZone = settings.RequireTimeZone;
            model.UseDoNotCall = settings.UseDoNotCall;
            model.UseDoNotSms = settings.UseDoNotSms;
            model.UseDoNotChat = settings.UseDoNotChat;
            model.UseDoNotEmail = settings.UseDoNotEmail;
            model.TimeZoneId = OmnichannelTimeZoneHelper.NormalizeTimeZoneId(_clock, part.TimeZoneId);
            model.AvailableTimeZones = OmnichannelTimeZoneHelper.GetTimeZoneOptions(_clock, S, "Select lead time zone", model.TimeZoneId);
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

        part.TimeZoneId = OmnichannelTimeZoneHelper.NormalizeTimeZoneId(_clock, model.TimeZoneId);

        if (settings.RequireTimeZone && string.IsNullOrEmpty(part.TimeZoneId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeZoneId), S["The contact time zone is required."]);
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
}
