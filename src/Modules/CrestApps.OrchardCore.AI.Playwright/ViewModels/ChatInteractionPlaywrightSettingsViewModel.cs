using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.AI.Playwright.ViewModels;

public class ChatInteractionPlaywrightSettingsViewModel
{
    public bool PlaywrightEnabled { get; set; }

    [MaxLength(256)]
    public string PlaywrightUsername { get; set; }

    [DataType(DataType.Password)]
    public string PlaywrightPassword { get; set; }

    public bool HasSavedPassword { get; set; }

    [MaxLength(1024)]
    public string PlaywrightBaseUrl { get; set; }

    [MaxLength(1024)]
    public string PlaywrightAdminBaseUrl { get; set; }

    [MaxLength(2048)]
    public string PlaywrightPersistentProfilePath { get; set; }

    public bool PlaywrightHeadless { get; set; }

    public bool PlaywrightPublishByDefault { get; set; }
}
