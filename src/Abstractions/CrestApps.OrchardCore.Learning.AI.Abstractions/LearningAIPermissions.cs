using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// The permissions of Learning AI. The learner tools need Access learning and the staff tools need the Learning
/// permissions of the records they read; these two cover the staff assistants that call a model.
/// </summary>
public static class LearningAIPermissions
{
    /// <summary>
    /// Ask the authoring assistant for draft outlines, questions, rubrics and translations.
    /// </summary>
    public static readonly Permission UseAuthoringAssistant = new("UseLearningAuthoringAssistant", LocalizationSource.Create("Use the learning authoring assistant", typeof(LearningAIPermissions)));

    /// <summary>
    /// Ask for a suggested score and feedback while grading.
    /// </summary>
    public static readonly Permission UseScoringHelp = new("UseLearningScoringHelp", LocalizationSource.Create("Use scoring help while grading", typeof(LearningAIPermissions)));
}
