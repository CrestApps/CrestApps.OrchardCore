using CrestApps.OrchardCore.Commerce.ViewModels;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// The bookkeeping of Commerce › Set up your store, shared by its page and its AI tools: the steps in order, which is
/// done or skipped, the step to do now, and recording a quick action that ran or a step skipped. It needs no
/// registration: callers pass the steps and the site service they already have.
/// </summary>
internal static class StoreSetupGuide
{
    /// <summary>
    /// Gets the steps in the guide's order: by <see cref="IStoreSetupStep.Order"/>, then by name.
    /// </summary>
    /// <param name="steps">The steps the enabled modules add.</param>
    /// <returns>The steps in order.</returns>
    public static IEnumerable<IStoreSetupStep> Order(IEnumerable<IStoreSetupStep> steps)
        => (steps ?? []).OrderBy(step => step.Order).ThenBy(step => step.Name, StringComparer.Ordinal);

    /// <summary>
    /// Finds a step by its name.
    /// </summary>
    /// <param name="steps">The steps the enabled modules add.</param>
    /// <param name="name">The step's name.</param>
    /// <returns>The step, or <see langword="null"/>.</returns>
    public static IStoreSetupStep Find(IEnumerable<IStoreSetupStep> steps, string name)
        => (steps ?? []).FirstOrDefault(step => string.Equals(step.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Describes every step as it stands, with the step to do now: the first that is neither done nor skipped, the
    /// required ones first.
    /// </summary>
    /// <param name="steps">The steps the enabled modules add.</param>
    /// <param name="progress">The guide's progress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The steps in order, and the step to do now.</returns>
    public static async Task<StoreSetupViewModel> DescribeAsync(IEnumerable<IStoreSetupStep> steps, StoreSetupSettings progress, CancellationToken cancellationToken = default)
    {
        progress ??= new StoreSetupSettings();

        var model = new StoreSetupViewModel();

        foreach (var step in Order(steps))
        {
            var description = await step.DescribeAsync(cancellationToken);
            var done = description.IsDone ?? progress.Completed.Contains(step.Name, StringComparer.Ordinal);

            model.Steps.Add(new StoreSetupStepViewModel
            {
                Name = step.Name,
                Description = description,
                IsDone = done,
                IsSkipped = !done && progress.Skipped.Contains(step.Name, StringComparer.Ordinal),
            });
        }

        model.Current = model.Steps.FirstOrDefault(step => !step.IsDone && !step.IsSkipped && !step.Description.IsOptional)?.Name
            ?? model.Steps.FirstOrDefault(step => !step.IsDone && !step.IsSkipped)?.Name;

        return model;
    }

    /// <summary>
    /// Runs a step's quick action and, when it succeeds, records the step as done and no longer skipped.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="values">The values entered in the step's fields, by field name.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How the quick action went, or <see langword="null"/> for a step without one.</returns>
    public static async Task<StoreSetupActionResult> RunAsync(IStoreSetupStep step, IReadOnlyDictionary<string, string> values, ISiteService siteService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        var result = await step.RunAsync(new StoreSetupActionContext { Values = values ?? new Dictionary<string, string>() }, cancellationToken);

        if (result?.Succeeded == true)
        {
            await UpdateProgressAsync(siteService, progress =>
            {
                if (!progress.Completed.Contains(step.Name, StringComparer.Ordinal))
                {
                    progress.Completed.Add(step.Name);
                }

                progress.Skipped.Remove(step.Name);
            });
        }

        return result;
    }

    /// <summary>
    /// Marks a step as skipped, or clears the skip.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="undo">Whether to clear the skip.</param>
    /// <param name="siteService">The site service.</param>
    /// <returns>The task.</returns>
    public static Task SkipAsync(IStoreSetupStep step, bool undo, ISiteService siteService)
    {
        ArgumentNullException.ThrowIfNull(step);

        return UpdateProgressAsync(siteService, progress =>
        {
            progress.Skipped.Remove(step.Name);

            if (!undo)
            {
                progress.Skipped.Add(step.Name);
            }
        });
    }

    private static async Task UpdateProgressAsync(ISiteService siteService, Action<StoreSetupSettings> update)
    {
        ArgumentNullException.ThrowIfNull(siteService);

        var site = await siteService.LoadSiteSettingsAsync();
        var progress = site.GetOrCreate<StoreSetupSettings>();

        update(progress);
        site.Put(progress);
        await siteService.UpdateSiteSettingsAsync(site);
    }
}
