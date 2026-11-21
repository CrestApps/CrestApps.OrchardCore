using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Credentials.Navigation;

/// <summary>
/// Adds Credential Definitions and Credential Awards to the Learning admin menu.
/// </summary>
internal sealed class CredentialsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialsAdminMenu(IStringLocalizer<CredentialsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Credential Definitions"], S["Credential Definitions"].PrefixPosition(), definitions => definitions
                .AddClass("learning-credential-definitions")
                .Id("learningCredentialDefinitions")
                .Action("List", "Admin", new { area = "OrchardCore.Contents", contentTypeId = CredentialsConstants.ContentTypes.CredentialDefinition })
                .Permission(LearningCredentialsPermissions.ManageCredentialDefinitions)
                .LocalNav())
            .Add(S["Credential Awards"], S["Credential Awards"].PrefixPosition(), awards => awards
                .AddClass("learning-credential-awards")
                .Id("learningCredentialAwards")
                .Action("Index", "CredentialAwardsAdmin", CredentialsConstants.Features.DigitalCredentials)
                .Permission(LearningCredentialsPermissions.ViewCredentialAwards)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
