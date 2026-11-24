namespace CrestApps.OrchardCore.Accounts.ViewModels;

/// <summary>
/// The values of the account team editor.
/// </summary>
public class AccountTeamEditViewModel
{
    public string OwnerStaffId { get; set; }

    public IList<string> TeamStaffIds { get; set; } = [];
}

/// <summary>
/// The account team shown on the account page.
/// </summary>
public class AccountTeamViewModel
{
    public string OwnerStaffId { get; set; }

    public string OwnerName { get; set; }

    public IList<KeyValuePair<string, string>> Team { get; set; } = [];
}
