using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Migrations;

/// <summary>
/// Creates the Certificate Template content type.
/// </summary>
/// <remarks>
/// The definitions are written in code, not with a recipe: a recipe runs its steps in a scope of its own, whose second
/// connection waits forever on SQLite for the write lock the migration's transaction holds.
/// </remarks>
internal sealed class CertificateTemplateMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CertificateTemplateMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CertificateTemplatePart), part => part
            .Attachable(false)
            .WithDisplayName("Certificate Template")
            .WithDescription("The page, colors, font, Liquid text blocks and QR code of a printed certificate.")
            .WithField("BackgroundImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Background image")
                .WithPosition("0")
                .WithSettings(new MediaFieldSettings
                {
                    Hint = "A JPEG or PNG stretched over the whole page, behind the text. Use the shape of the page: 11 by 8.5 inches for Letter on its side.",
                    Multiple = false,
                    AllowedExtensions = [".jpg", ".jpeg", ".png"],
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(SkillsConstants.CertificateTemplateContentType, type => type
            .WithDisplayName("Certificate Template")
            .WithDescription("How the certificates the site issues are printed.")
            .Creatable()
            .Listable()
            .Draftable(false)
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(nameof(CertificateTemplatePart), part => part.WithPosition("1")));

        return 1;
    }

    // The settings of Orchard Core's media field, by the name they are stored under, without a reference to the Media
    // module's own type.
    private sealed class MediaFieldSettings
    {
        public string Hint { get; set; }

        public bool Required { get; set; }

        public bool Multiple { get; set; }

        public bool AllowMediaText { get; set; }

        public string[] AllowedExtensions { get; set; } = [];
    }
}
