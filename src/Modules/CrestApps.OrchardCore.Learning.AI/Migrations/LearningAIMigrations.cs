using CrestApps.OrchardCore.Learning.AI.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.AI.Migrations;

/// <summary>
/// Adds the learning assistant switch to the course type.
/// </summary>
internal sealed class LearningAIMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAIMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public LearningAIMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the part and attaches it to the course type.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(LearningAssistantPart), part => part
            .WithDisplayName("Learning assistant")
            .WithDescription("Lets the learning tutor answer the course's enrolled learners from its lessons. Off by default."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.ContentTypes.Course, type => type
            .WithPart(nameof(LearningAssistantPart), part => part.WithPosition("90")));

        return 1;
    }
}
