using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Certifications.Navigation;

/// <summary>
/// Adds My certifications and My education credits to the storefront account menu, after My learning.
/// </summary>
internal sealed class CertificationsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public CertificationsAccountMenu(IStringLocalizer<CertificationsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["My certifications"], "30.6", certifications => certifications
                .Id("account-learning-certifications")
                .AddClass("fa-solid fa-certificate")
                .Url("~/learning/certifications")
                .Permission(LearningPermissions.AccessLearning))
            .Add(S["My education credits"], "30.8", credits => credits
                .Id("account-learning-credits")
                .AddClass("fa-solid fa-coins")
                .Url("~/learning/credits")
                .Permission(LearningPermissions.AccessLearning));

        return ValueTask.CompletedTask;
    }
}
