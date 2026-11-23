using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Mailing Lists",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MailingListsConstants.Features.MailingLists,
    Name = "Mailing Lists",
    Description = "Mailing lists people sign up to, with double opt-in by e-mail, the evidence of each person's consent, unsubscribe links on the e-mail preferences page, and a provider seam that keeps a list at an outside e-mail marketing service.",
    Category = "Communication",
    Dependencies =
    [
        ConsentConstants.Features.Consent,
        ResourcesConstants.Feature.Area,
        OrchardCoreFeatureIds.Email,
    ]
)]

[assembly: Feature(
    Id = MailingListsConstants.Features.SignupWidget,
    Name = "Mailing List Sign-up Widget",
    Description = "A newsletter sign-up widget that adds visitors to one or more mailing lists, protected by a trap field, the site's challenge and a rate limit.",
    Category = "Communication",
    Dependencies =
    [
        MailingListsConstants.Features.MailingLists,
        OrchardCoreFeatureIds.Widgets,
        OrchardCoreFeatureIds.RateLimits,
    ]
)]

[assembly: Feature(
    Id = MailingListsConstants.Features.WelcomeCoupon,
    Name = "Mailing List Welcome Coupon",
    Description = "Gives each new address of a mailing list a one-time code of a coupon, at sign-up or once the address is verified, delivered by e-mail.",
    Category = "Communication",
    Dependencies =
    [
        MailingListsConstants.Features.MailingLists,
        CheckoutConstants.Features.Area,
    ]
)]
