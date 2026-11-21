using CrestApps.OrchardCore.Accounts.Models;

namespace CrestApps.OrchardCore.Accounts.ViewModels;

public class InviteToAccountTaskViewModel
{
    public string AccountId { get; set; }

    public string Email { get; set; }

    public AccountMemberRole Role { get; set; }
}
