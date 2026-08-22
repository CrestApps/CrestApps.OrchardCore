using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the voice media library entry to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterVoiceMediaAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterVoiceMediaAdminMenu(IStringLocalizer<ContactCenterVoiceMediaAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Management"], "100", management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Voice Media"], S["Voice Media"].PrefixPosition(), voiceMedia => voiceMedia
                    .AddClass("voice-media")
                    .Id("voiceMedia")
                    .Action("Index", "VoiceMedia", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageVoiceMedia)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}
