using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Learning.Surveys.Core;

/// <summary>
/// Creates the survey a course survey or choice owns when the item is first saved, and keeps the survey's title in step
/// with the item's.
/// </summary>
public sealed class CourseSurveyPartHandler : ContentPartHandler<CourseSurveyPart>
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseSurveyPartHandler"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public CourseSurveyPartHandler(
        ILearningSurveyService learningSurveys,
        IContentDefinitionManager contentDefinitionManager)
    {
        _learningSurveys = learningSurveys;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreateContentContext context, CourseSurveyPart part)
        => EnsureSurveyAsync(part);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdateContentContext context, CourseSurveyPart part)
        => EnsureSurveyAsync(part);

    private async Task EnsureSurveyAsync(CourseSurveyPart part)
    {
        if (!string.IsNullOrEmpty(part.SurveyId))
        {
            return;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var settings = definition?.Parts
            .FirstOrDefault(candidate => string.Equals(candidate.PartDefinition.Name, nameof(CourseSurveyPart), StringComparison.Ordinal))
            ?.GetSettings<CourseSurveyPartSettings>();
        var template = settings?.SingleQuestion == true ? SurveyTemplateProvider.Poll : SurveyTemplateProvider.CourseFeedback;
        var kind = settings?.SingleQuestion == true ? SurveyKinds.Poll : SurveyKinds.Survey;
        var survey = await _learningSurveys.CreateOwnedSurveyAsync(part.ContentItem, kind, template);

        part.SurveyId = survey?.ItemId;
        part.ContentItem.Apply(part);
    }
}
