using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// The authoring services of one session.
/// </summary>
internal sealed class AuthoringServices
{
    public SurveyManager Manager { get; init; }

    public SurveyPublisher Publisher { get; init; }

    public SurveyDefinitionValidator Validator { get; init; }

    public ISurveyControlRegistry Registry { get; init; }

    public SurveyDraftEditor DraftEditor { get; init; }

    public SurveyTemplateProvider Templates { get; init; }

    public SurveyStore SurveyStore { get; init; }

    public SurveyVersionStore VersionStore { get; init; }

    public SurveyCollectorStore CollectorStore { get; init; }

    public BankQuestionStore BankQuestionStore { get; init; }
}
