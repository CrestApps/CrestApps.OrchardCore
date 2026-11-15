namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The collection settings card of a party's page.
/// </summary>
public class PartyCollectionSettingsViewModel
{
    public string Party { get; set; }

    public bool RemindersPaused { get; set; }

    public bool LateFeesExempt { get; set; }

    public bool CanManage { get; set; }

    public string ReturnUrl { get; set; }
}
