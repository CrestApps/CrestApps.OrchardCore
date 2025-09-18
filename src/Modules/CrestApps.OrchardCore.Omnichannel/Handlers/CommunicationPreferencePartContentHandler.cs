using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Handlers;

internal sealed class CommunicationPreferencePartContentHandler : ContentDisplayDriver
{
    private readonly IClock _clock;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly HashSet<string> _contactContentTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _nonContactContentTypes = new(StringComparer.OrdinalIgnoreCase);

    public CommunicationPreferencePartContentHandler(
        IClock clock,
        IContentDefinitionManager contentDefinitionManager)
    {
        _clock = clock;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem contentItem, BuildEditorContext context)
    {
        if (_nonContactContentTypes.Contains(contentItem.ContentType))
        {
            return null;
        }

        if (!_contactContentTypes.Contains(contentItem.ContentType))
        {
            var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

            if (!typeDefinition.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelContact))
            {
                return null;
            }

            _contactContentTypes.Add(contentItem.ContentType);
        }

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
        if (_nonContactContentTypes.Contains(contentItem.ContentType))
        {
            return null;
        }

        if (!_contactContentTypes.Contains(contentItem.ContentType))
        {
            var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

            if (!typeDefinition.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelContact))
            {
                return null;
            }

            _contactContentTypes.Add(contentItem.ContentType);
        }

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
            part.DoNotChat = model.DoNotEmail;
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
