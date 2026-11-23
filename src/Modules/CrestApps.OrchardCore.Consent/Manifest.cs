using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Consent;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "E-mail Preferences",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ConsentConstants.Features.Consent,
    Name = "E-mail Preferences",
    Description = "Gives every marketing e-mail one unsubscribe link that opens the site's e-mail preferences page. A person can stop the list or campaign the e-mail came from, or all marketing e-mail, and mail apps can unsubscribe with one click. A \"stop all marketing\" is kept for the address, also when the person is not a customer or a contact.",
    Category = "Communication",
    EnabledByDependencyOnly = true
)]
