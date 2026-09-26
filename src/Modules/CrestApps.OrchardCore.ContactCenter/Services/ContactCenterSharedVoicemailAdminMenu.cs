using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the queue shared voicemail page to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterSharedVoicemailAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSharedVoicemailAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSharedVoicemailAdminMenu(IStringLocalizer<ContactCenterSharedVoicemailAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // The permission only shows the page; which queues' messages it lists is decided per queue when it is opened.
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Shared voicemail"], S["Shared voicemail"].PrefixPosition("3"), sharedVoicemail => sharedVoicemail
                    .AddClass("contact-center-shared-voicemail")
                    .Id("contactCenterSharedVoicemail")
                    .Action("Index", "SharedVoicemail", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.AccessSharedVoicemail)
                    .LocalNav()
                ),
                priority: 2);

        return ValueTask.CompletedTask;
    }
}
