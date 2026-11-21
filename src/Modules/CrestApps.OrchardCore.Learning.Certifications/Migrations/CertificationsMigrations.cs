using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Certifications.Migrations;

/// <summary>
/// Creates the tables of certification paths and education credits, and puts the education credits part on courses.
/// </summary>
internal sealed class CertificationsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CertificationsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await CertificationsIndexSchema.CreateAsync(SchemaBuilder);
        await CreateCreditsAsync();

        return 2;
    }

    /// <summary>
    /// Adds education credits: credit types, external learning, the credit ledger and the part on courses.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateCreditsAsync();

        return 2;
    }

    private async Task CreateCreditsAsync()
    {
        await EducationCreditsIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(EducationCreditsPart), part => part
            .Attachable()
            .WithDisplayName("Education Credits")
            .WithDescription("The education credits, such as CPD hours, earned by completing the content item: a course or a learning path."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.ContentTypes.Course, type => type
            .WithPart(nameof(EducationCreditsPart), part => part.WithPosition("20")));
    }
}
