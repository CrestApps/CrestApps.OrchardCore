using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.ViewModels;

/// <summary>
/// The Agreements tab of an account.
/// </summary>
public class AccountAgreementsViewModel
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets its agreements, newest first, with how their hours stand.
    /// </summary>
    public IReadOnlyList<(Agreement Agreement, AgreementBalance Balance)> Agreements { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may add one.
    /// </summary>
    public bool CanManage { get; set; }
}
