using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Credentials.Navigation;

/// <summary>
/// Adds My credentials to the storefront account menu, after My learning.
/// </summary>
internal sealed class CredentialsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsAccountMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialsAccountMenu(IStringLocalizer<CredentialsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["My credentials"], "31", credentials => credentials
            .Id("account-credentials")
            .AddClass("fa-solid fa-award")
            .Url("~/learning/credentials")
            .Permission(LearningPermissions.AccessLearning));

        return ValueTask.CompletedTask;
    }
}
