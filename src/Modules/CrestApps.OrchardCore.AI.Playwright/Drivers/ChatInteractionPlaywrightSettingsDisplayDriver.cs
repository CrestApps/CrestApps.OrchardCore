using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using CrestApps.OrchardCore.AI.Playwright.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Playwright.Drivers;

internal sealed class ChatInteractionPlaywrightSettingsDisplayDriver : DisplayDriver<ChatInteraction>
{
    internal readonly IStringLocalizer S;

    public ChatInteractionPlaywrightSettingsDisplayDriver(IStringLocalizer<ChatInteractionPlaywrightSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<ChatInteractionPlaywrightSettingsViewModel>("ChatInteractionPlaywrightSettings_Edit", model =>
        {
            var metadata = interaction.As<PlaywrightSessionMetadata>() ?? new PlaywrightSessionMetadata();

            model.PlaywrightEnabled = metadata.Enabled;
            model.PlaywrightUsername = metadata.Username;
            model.HasSavedPassword = !string.IsNullOrWhiteSpace(metadata.ProtectedPassword);
            model.PlaywrightBaseUrl = metadata.BaseUrl;
            model.PlaywrightAdminBaseUrl = metadata.AdminBaseUrl;
            model.PlaywrightPersistentProfilePath = metadata.PersistentProfilePath;
            model.PlaywrightHeadless = metadata.Headless;
            model.PlaywrightPublishByDefault = metadata.PublishByDefault;
        }).Location("Parameters:6#Settings;1");
    }
}
