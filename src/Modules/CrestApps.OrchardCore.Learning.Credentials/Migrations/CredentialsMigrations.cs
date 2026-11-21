using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Media.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Migrations;

/// <summary>
/// Creates the credential award index table, the credential definition part and the Credential definition type.
/// </summary>
internal sealed class CredentialsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public CredentialsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the index table, the part and the type.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await CredentialsIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CredentialDefinitionPart), part => part
            .Attachable(false)
            .WithDisplayName("Credential definition")
            .WithDescription("What an Open Badges 3.0 credential says its holder achieved (description, criteria, type, tags, alignments, validity) and the rules that award it."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(CredentialsConstants.ContentTypes.CredentialDefinition, part => part
            .WithField("Image", field => field
                .OfType("MediaField")
                .WithDisplayName("Image")
                .WithPosition("1")
                .WithSettings(new MediaFieldSettings
                {
                    Multiple = false,
                    Hint = "The badge image shown in wallets and on the verification page. A square PNG or SVG works best.",
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(CredentialsConstants.ContentTypes.CredentialDefinition, type => type
            .WithDisplayName("Credential definition")
            .WithDescription("A digital credential: the achievement it states and when it is awarded.")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(CredentialsConstants.ContentTypes.CredentialDefinition, part => part.WithPosition("2"))
            .WithPart(nameof(CredentialDefinitionPart), part => part.WithPosition("3")));

        return 1;
    }
}
