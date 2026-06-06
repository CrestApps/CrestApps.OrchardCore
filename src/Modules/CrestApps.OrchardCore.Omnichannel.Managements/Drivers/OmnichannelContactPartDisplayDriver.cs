using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactPartDisplayDriver : ContentPartDisplayDriver<OmnichannelContactPart>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public OmnichannelContactPartDisplayDriver(IClock clock)
    {
        _clock = clock;
    }

    public override IDisplayResult Edit(OmnichannelContactPart part, BuildPartEditorContext context)
    {
        return Initialize<OmnichannelContactPartViewModel>(GetEditorShapeType(context), model =>
        {
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

        var utcNow = _clock.UtcNow;
        part.SetDoNotCall(model.DoNotCall, utcNow);
        part.SetDoNotSms(model.DoNotSms, utcNow);
        part.SetDoNotChat(model.DoNotChat, utcNow);
        part.SetDoNotEmail(model.DoNotEmail, utcNow);

        return Edit(part, context);
    }
}
