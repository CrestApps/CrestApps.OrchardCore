using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Learning.Surveys.Core;

/// <summary>
/// Creates the survey a course survey or choice owns when the item is first saved, and keeps the survey's title in step
/// with the item's. A copied item gets a copy of the original's survey.
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

    /// <inheritdoc />
    public override async Task CloningAsync(CloneContentContext context, CourseSurveyPart part)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CloneContentItem is null || !context.CloneContentItem.TryGet<CourseSurveyPart>(out var clone))
        {
            return;
        }

        // A copied course survey gets a copy of the original's survey, never the original itself.
        var (kind, template) = await KindAndTemplateAsync(context.CloneContentItem.ContentType);
        var survey = await _learningSurveys.CopyOwnedSurveyAsync(context.CloneContentItem, part.SurveyId, kind, template);

        clone.SurveyId = survey?.ItemId;
        context.CloneContentItem.Apply(clone);
    }

    private async Task EnsureSurveyAsync(CourseSurveyPart part)
    {
        if (!string.IsNullOrEmpty(part.SurveyId))
        {
            return;
        }

        var (kind, template) = await KindAndTemplateAsync(part.ContentItem.ContentType);
        var survey = await _learningSurveys.CreateOwnedSurveyAsync(part.ContentItem, kind, template);

        part.SurveyId = survey?.ItemId;
        part.ContentItem.Apply(part);
    }

    private async Task<(string Kind, string Template)> KindAndTemplateAsync(string contentType)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var settings = definition?.Parts
            .FirstOrDefault(candidate => string.Equals(candidate.PartDefinition.Name, nameof(CourseSurveyPart), StringComparison.Ordinal))
            ?.GetSettings<CourseSurveyPartSettings>();

        return settings?.SingleQuestion == true
            ? (SurveyKinds.Poll, SurveyTemplateNames.Poll)
            : (SurveyKinds.Survey, LearningSurveysConstants.CourseFeedbackTemplate);
    }
}
