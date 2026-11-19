namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The Service level card of an account's page.
/// </summary>
public class AccountServiceLevelViewModel
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the account's own ticket policy, if it has one.
    /// </summary>
    public string AccountPolicyId { get; set; }

    /// <summary>
    /// Gets or sets the name of the account's own ticket policy, if it has one.
    /// </summary>
    public string AccountPolicyName { get; set; }

    /// <summary>
    /// Gets the agreements in force that name a ticket policy; theirs comes first for the tickets they cover.
    /// </summary>
    public List<AccountServiceLevelAgreement> Agreements { get; } = [];

    /// <summary>
    /// Gets or sets whether the user may change the account's own policy (edit the account).
    /// </summary>
    public bool CanChange { get; set; }

    /// <summary>
    /// Gets or sets whether the user may open the policies.
    /// </summary>
    public bool CanOpenPolicies { get; set; }
}

/// <summary>
/// An agreement in force that names a ticket policy.
/// </summary>
/// <param name="AgreementId">The agreement.</param>
/// <param name="Number">Its number.</param>
/// <param name="Name">Its name.</param>
/// <param name="PolicyId">The policy.</param>
/// <param name="PolicyName">The policy's name.</param>
public sealed record AccountServiceLevelAgreement(string AgreementId, string Number, string Name, string PolicyId, string PolicyName);
