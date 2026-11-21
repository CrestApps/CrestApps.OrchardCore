using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when a member of the course staff approved the learner's completion.
/// </summary>
public sealed class StaffApprovalCriterion : ICourseCompletionCriterion
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.StaffApproval;

    /// <inheritdoc />
    public Task<bool> IsMetAsync(CompletionCriterionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var properties = context.Enrollment?.Properties;

        return Task.FromResult(properties is not null && properties.ContainsKey(LearningRuleNames.StaffApprovalProperty));
    }
}
