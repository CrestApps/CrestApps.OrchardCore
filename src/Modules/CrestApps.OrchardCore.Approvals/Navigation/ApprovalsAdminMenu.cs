using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Approvals.Navigation;

/// <summary>
/// Adds Approvals to the admin menu: the requests and the policies.
/// </summary>
public sealed class ApprovalsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalsAdminMenu(IStringLocalizer<ApprovalsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Approvals"], S["Approvals"].PrefixPosition("7"), approvals => approvals
            .Id("approvals")
            .AddClass("approvals")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-stamp")
            .Add(S["Requests"], S["Requests"].PrefixPosition("10"), requests => requests
                .Id("approvalRequests")
                .Action("Index", "ApprovalRequests", ApprovalsConstants.Features.Approvals)
                .LocalNav())
            .Add(S["Policies"], S["Policies"].PrefixPosition("20"), policies => policies
                .Id("approvalPolicies")
                .Action("Index", "ApprovalPolicies", ApprovalsConstants.Features.Approvals)
                .Permission(ApprovalsPermissions.ManageApprovalPolicies)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
