using CrestApps.OrchardCore.Learning.Certifications.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Migrations;

/// <summary>
/// Defines the competencies part and puts it on courses, and on learning paths when Learning Programs defined them.
/// </summary>
[Feature(CertificationsConstants.Features.Competencies)]
internal sealed class CompetenciesMigrations : DataMigration
{
    private const string LearningPathContentType = "LearningPath";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CompetenciesMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CompetenciesPart), part => part
            .Attachable()
            .WithDisplayName("Competencies")
            .WithDescription("The competencies (Skills skills) a course or a learning path grants at a level when a learner completes it."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.ContentTypes.Course, type => type
            .WithPart(nameof(CompetenciesPart), part => part.WithPosition("21")));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(LearningPathContentType) is not null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningPathContentType, type => type
                .WithPart(nameof(CompetenciesPart), part => part.WithPosition("21")));
        }

        return 1;
    }
}
