using CrestApps.OrchardCore.Learning.Certifications.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Adds Education credits and External learning to the Learning menu.
/// </summary>
internal sealed class CreditsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public CreditsAdminMenu(IStringLocalizer<CreditsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Education Credits"], S["Education Credits"].PrefixPosition(), credits => credits
                .AddClass("education-credits")
                .Id("educationCredits")
                .Action("Index", "EducationCreditTypes", CertificationsConstants.Features.Certifications)
                .Permission(LearningCertificationsPermissions.ManageEducationCredits)
                .LocalNav())
            .Add(S["External Learning"], S["External Learning"].PrefixPosition(), external => external
                .AddClass("external-learning")
                .Id("externalLearning")
                .Action("Index", "ExternalLearning", CertificationsConstants.Features.Certifications)
                .Permission(LearningCertificationsPermissions.ReviewExternalLearning)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
