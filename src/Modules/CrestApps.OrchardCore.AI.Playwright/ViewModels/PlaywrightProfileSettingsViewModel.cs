using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.AI.Playwright.ViewModels;

public class PlaywrightProfileSettingsViewModel
{
    public bool Enabled { get; set; }

    [MaxLength(256)]
    public string Username { get; set; }

    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool HasSavedPassword { get; set; }

    [MaxLength(1024)]
    public string BaseUrl { get; set; }

    [MaxLength(1024)]
    public string AdminBaseUrl { get; set; }

    [MaxLength(2048)]
    public string PersistentProfilePath { get; set; }

    public bool Headless { get; set; }

    public bool PublishByDefault { get; set; }
}
