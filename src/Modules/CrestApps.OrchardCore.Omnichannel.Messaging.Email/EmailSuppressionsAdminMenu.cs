using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email;

/// <summary>
/// Adds the email suppression list beside the omnichannel addresses, under Interaction Center → Management.
/// </summary>
internal sealed class EmailSuppressionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public EmailSuppressionsAdminMenu(IStringLocalizer<EmailSuppressionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Email Suppressions"], S["Email Suppressions"].PrefixPosition(), suppressions => suppressions
                        .AddClass("email-suppressions")
                        .Id("emailSuppressions")
                        .Action("Index", "Suppressions", "CrestApps.OrchardCore.Omnichannel.Messaging.Email")
                        .Permission(OmnichannelConstants.Permissions.ManageChannelEndpoints)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
