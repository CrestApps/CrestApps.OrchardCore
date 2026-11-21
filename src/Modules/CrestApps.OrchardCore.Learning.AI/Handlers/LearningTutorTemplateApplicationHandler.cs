using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Learning.AI.Models;

namespace CrestApps.OrchardCore.Learning.AI.Handlers;

/// <summary>
/// Marks a profile built from the learning tutor template as a tutor, so its retrieval is limited to the learner's
/// enrolled courses from the start.
/// </summary>
internal sealed class LearningTutorTemplateApplicationHandler : IAIProfileTemplateApplicationHandler
{
    /// <inheritdoc />
    public Task AppliedAsync(AIProfileTemplateAppliedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.Equals(context.Template.Name, LearningAIConstants.Templates.Tutor, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(context.Template.ItemId, LearningAIConstants.Templates.Tutor, StringComparison.OrdinalIgnoreCase))
        {
            context.Profile.Put(new LearningTutorProfileMetadata { IsTutor = true });
        }

        return Task.CompletedTask;
    }
}
