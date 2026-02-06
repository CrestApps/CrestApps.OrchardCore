using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Handlers;

internal sealed class CommunicationPreferencePartContentHandler : ContentDisplayDriver
{
    private readonly IClock _clock;

    public CommunicationPreferencePartContentHandler(IClock clock)
    {
        _clock = clock;
    }

    public override bool CanHandleModel(ContentItem contentItem)
    {
        return contentItem.Has<OmnichannelContactPart>();
    }

    public override IDisplayResult Edit(ContentItem contentItem, BuildEditorContext context)
    {
        return Initialize<CommunicationPreferenceViewModel>("CommunicationPreference_Edit", model =>
        {
            var part = contentItem.As<CommunicationPreferencePart>();

            model.DoNotChat = part.DoNotChat;
            model.DoNotChatUtc = part.DoNotChatUtc;

            model.DoNotSms = part.DoNotSms;
            model.DoNotSmsUtc = part.DoNotSmsUtc;

            model.DoNotEmail = part.DoNotEmail;
            model.DoNotEmailUtc = part.DoNotEmailUtc;

            model.DoNotCall = part.DoNotCall;
            model.DoNotCallUtc = part.DoNotCallUtc;
        }).Location("Parts:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        var model = new CommunicationPreferenceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var part = contentItem.As<CommunicationPreferencePart>();

        if (!part.DoNotCall && model.DoNotCall)
        {
            part.DoNotCall = model.DoNotCall;
            part.DoNotCallUtc = _clock.UtcNow;
        }
        else
        {
            part.DoNotCall = false;
            part.DoNotCallUtc = null;
        }

        if (!part.DoNotEmail && model.DoNotEmail)
        {
            part.DoNotEmail = model.DoNotEmail;
            part.DoNotEmailUtc = _clock.UtcNow;
        }
        else
        {
            part.DoNotEmail = false;
            part.DoNotEmailUtc = null;
        }

        if (!part.DoNotChat && model.DoNotChat)
        {
            part.DoNotChat = model.DoNotChat;
            part.DoNotChatUtc = _clock.UtcNow;
        }
        else
        {
            part.DoNotChat = false;
            part.DoNotChatUtc = null;
        }

        if (!part.DoNotSms && model.DoNotSms)
        {
            part.DoNotSms = model.DoNotSms;
            part.DoNotSmsUtc = _clock.UtcNow;
        }
        else
        {
            part.DoNotSms = false;
            part.DoNotSmsUtc = null;
        }

        contentItem.Apply(part);

        return Edit(contentItem, context);
    }
}
