using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Met when a named course item, such as the final exam, is passed with a result computed on the server.
/// </summary>
public sealed class ItemPassedCriterion : ICourseCompletionCriterion
{
    /// <inheritdoc />
    public string Name => LearningRuleNames.ItemPassed;

    /// <inheritdoc />
    public Task<bool> IsMetAsync(CompletionCriterionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var itemId = context.Settings?.Get("ItemId");
        var progress = itemId is null ? null : context.Progress?.Find(itemId);

        return Task.FromResult(progress is { Passed: true } && !progress.SelfReported);
    }
}
