using CrestApps.OrchardCore.Accounts.Models;

namespace CrestApps.OrchardCore.Accounts.ViewModels;

public class AccountInvitationViewModel
{
    public string Token { get; set; }

    public AccountInvitation Invitation { get; set; }

    public string AccountName { get; set; }

    public bool IsValid { get; set; }

    public bool IsExpired { get; set; }

    public bool NeedsEmailConfirmation { get; set; }
}

public class AccountMembersViewModel
{
    public string AccountName { get; set; }

    public bool IsAdmin { get; set; }

    public IReadOnlyList<AccountMember> Members { get; set; } = [];

    public IReadOnlyList<AccountInvitation> Invitations { get; set; } = [];
}
