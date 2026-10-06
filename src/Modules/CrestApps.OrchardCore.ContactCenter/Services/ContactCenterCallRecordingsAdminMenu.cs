using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the call recordings page to the Contact Center admin navigation.
/// </summary>
public sealed class ContactCenterCallRecordingsAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterCallRecordingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterCallRecordingsAdminMenu(IStringLocalizer<ContactCenterCallRecordingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Shown to anyone who may hear their own calls; the page lists everybody's only to those who may hear them.
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Call recordings"], S["Call recordings"].PrefixPosition("3"), recordings => recordings
                    .AddClass("contact-center-call-recordings")
                    .Id("contactCenterCallRecordings")
                    .Action("Index", "CallRecordings", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ListenToOwnCallRecordings)
                    .LocalNav()
                ),
                priority: 2);

        return ValueTask.CompletedTask;
    }
}
