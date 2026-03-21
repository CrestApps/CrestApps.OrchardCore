using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using CrestApps.OrchardCore.AI.Playwright.Settings;
using CrestApps.OrchardCore.AI.Playwright.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Playwright.Drivers;

/// <summary>
/// Adds Playwright configuration to the AI Profile editor.
/// </summary>
public class PlaywrightProfileSettingsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    internal readonly IStringLocalizer S;

    public PlaywrightProfileSettingsDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<PlaywrightProfileSettingsDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<PlaywrightProfileSettingsViewModel>("PlaywrightProfileSettings_Edit", model =>
        {
            var metadata = profile.As<PlaywrightSessionMetadata>() ?? new PlaywrightSessionMetadata();
            var legacySettings = profile.GetSettings<PlaywrightProfileSettings>();

            model.Enabled = metadata.Enabled || legacySettings.Enabled;
            model.Username = metadata.Username;
            model.HasSavedPassword = !string.IsNullOrWhiteSpace(metadata.ProtectedPassword);
            model.BaseUrl = metadata.BaseUrl;
            model.AdminBaseUrl = metadata.AdminBaseUrl;
            model.PersistentProfilePath = metadata.PersistentProfilePath;
            model.Headless = metadata.Headless;
            model.PublishByDefault = metadata.PublishByDefault;
        }).Location("Content:50#PlaywrightBrowserAutomation;10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new PlaywrightProfileSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var existingMetadata = profile.As<PlaywrightSessionMetadata>() ?? new PlaywrightSessionMetadata();
        var protector = _dataProtectionProvider.CreateProtector(PlaywrightConstants.ProtectorName);
        var protectedPassword = existingMetadata.ProtectedPassword;

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            protectedPassword = protector.Protect(model.Password);
        }

        profile.Put(new PlaywrightSessionMetadata
        {
            Enabled = model.Enabled,
            BrowserMode = PlaywrightBrowserMode.PersistentContext,
            Username = model.Username?.Trim(),
            ProtectedPassword = protectedPassword,
            BaseUrl = model.BaseUrl?.Trim(),
            AdminBaseUrl = model.AdminBaseUrl?.Trim(),
            PersistentProfilePath = model.PersistentProfilePath?.Trim(),
            Headless = model.Headless,
            PublishByDefault = model.PublishByDefault,
        });

        profile.AlterSettings<PlaywrightProfileSettings>(settings =>
        {
            settings.Enabled = model.Enabled;
        });

        return Edit(profile, context);
    }
}
