using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Defines the Survey part and the Survey widget.
/// </summary>
internal sealed class SurveyEmbeddingMigrations : DataMigration
{
    /// <summary>
    /// The content type of the survey widget.
    /// </summary>
    public const string WidgetType = "SurveyWidget";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEmbeddingMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SurveyEmbeddingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the part and the widget.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(SurveyPart), part => part
            .Attachable()
            .WithDisplayName("Survey")
            .WithDescription("Shows a survey in the page, as a button or as a link."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(WidgetType, type => type
            .WithDisplayName("Survey")
            .WithDescription("A survey shown in a zone of the site.")
            .Stereotype("Widget")
            .WithPart(nameof(SurveyPart)));

        return 1;
    }
}
