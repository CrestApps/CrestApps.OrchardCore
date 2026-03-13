using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Playwright.Handlers;

internal sealed class PlaywrightChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public PlaywrightChatInteractionSettingsHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings)
    {
        var existingMetadata = interaction.As<PlaywrightSessionMetadata>() ?? new PlaywrightSessionMetadata();
        var protector = _dataProtectionProvider.CreateProtector(PlaywrightConstants.ProtectorName);
        var password = GetString(settings, "playwrightPassword");
        var protectedPassword = string.IsNullOrWhiteSpace(password)
            ? existingMetadata.ProtectedPassword
            : protector.Protect(password);

        interaction.Put(new PlaywrightSessionMetadata
        {
            Enabled = GetBool(settings, "playwrightEnabled"),
            BrowserMode = PlaywrightBrowserMode.PersistentContext,
            Username = GetString(settings, "playwrightUsername")?.Trim(),
            ProtectedPassword = protectedPassword,
            BaseUrl = GetString(settings, "playwrightBaseUrl")?.Trim(),
            AdminBaseUrl = GetString(settings, "playwrightAdminBaseUrl")?.Trim(),
            PersistentProfilePath = GetString(settings, "playwrightPersistentProfilePath")?.Trim(),
            Headless = GetBool(settings, "playwrightHeadless"),
            PublishByDefault = GetBool(settings, "playwrightPublishByDefault"),
        });

        return Task.CompletedTask;
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings)
        => Task.CompletedTask;

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return string.Equals(prop.GetString(), "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
