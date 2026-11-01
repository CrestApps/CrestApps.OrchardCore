namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Checks a schedule before it is published, such as against labor rules. A violation that blocks stops the publish;
/// one that needs an override stops it unless a reason is given.
/// </summary>
public interface ISchedulePublishValidator
{
    /// <summary>
    /// Checks the schedule and adds what is broken to the context.
    /// </summary>
    /// <param name="context">The schedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ValidateAsync(SchedulePublishValidationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the publish goes ahead after every validator has checked it, such as to record the overrides of
    /// the rules gone ahead with.
    /// </summary>
    /// <param name="context">The schedule and what was found.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ProceedingAsync(SchedulePublishValidationContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
