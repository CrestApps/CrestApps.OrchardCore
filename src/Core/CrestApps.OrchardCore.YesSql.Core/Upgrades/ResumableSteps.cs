using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.YesSql.Core.Upgrades;

/// <summary>
/// Work an upgrade or a change asks for that is too large for the request or the tenant's activation that started it,
/// such as giving every open record a timer or moving every record's notes. A migration or a handler asks for a step by
/// name (<see cref="ResumableSteps.RequestAsync"/>); a background task runs one batch of each pending step every minute,
/// keeping its cursor, until the step says it is done. A run that stops part way, or a step that throws, is simply run
/// again, so a step must be safe to repeat.
/// </summary>
public interface IResumableStep
{
    /// <summary>
    /// Gets the step's name, such as <c>WorkOrders.ServiceLevelTimers</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs one batch of the step.
    /// </summary>
    /// <param name="context">The step's cursor and data, kept between runs.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the step is done.</returns>
    Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a resumable step keeps between its runs.
/// </summary>
public sealed class ResumableStepContext
{
    /// <summary>
    /// Gets or sets where the step got to, such as the last document id it handled; empty on its first run.
    /// </summary>
    public string Cursor { get; set; }

    /// <summary>
    /// Gets or sets the data the step was asked with, by key, such as the schedules whose hours changed.
    /// </summary>
    public IDictionary<string, string> Data { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// The pending resumable steps of a tenant.
/// </summary>
public sealed class ResumableStepsDocument : Document
{
    /// <summary>
    /// Gets or sets the pending steps, by name.
    /// </summary>
    public Dictionary<string, ResumableStepContext> Pending { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Asks for resumable steps and keeps their progress.
/// </summary>
public sealed class ResumableSteps
{
    private readonly IDocumentManager<ResumableStepsDocument> _documents;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumableSteps"/> class.
    /// </summary>
    /// <param name="documents">The document that keeps the pending steps.</param>
    public ResumableSteps(IDocumentManager<ResumableStepsDocument> documents)
    {
        _documents = documents;
    }

    /// <summary>
    /// Asks for a step. Asking again for a pending step keeps its cursor and adds the data.
    /// </summary>
    /// <param name="name">The step's name.</param>
    /// <param name="data">Data for the step, by key, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    public async Task RequestAsync(string name, IReadOnlyDictionary<string, string> data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _documents.GetOrCreateMutableAsync();

        if (!document.Pending.TryGetValue(name, out var context))
        {
            document.Pending[name] = context = new ResumableStepContext();
        }

        foreach (var (key, value) in data ?? new Dictionary<string, string>())
        {
            context.Data[key] = value;
        }

        await _documents.UpdateAsync(document);
    }

    /// <summary>
    /// Returns whether a step is pending.
    /// </summary>
    /// <param name="name">The step's name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public async Task<bool> IsPendingAsync(string name)
        => (await _documents.GetOrCreateImmutableAsync()).Pending.ContainsKey(name);

    /// <summary>
    /// Runs one batch of each pending step that has a registered <see cref="IResumableStep"/>, keeps the cursors, and
    /// drops the steps that are done.
    /// </summary>
    /// <param name="steps">The registered steps.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many steps ran.</returns>
    public async Task<int> RunPendingAsync(IEnumerable<IResumableStep> steps, ILogger logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var pending = (await _documents.GetOrCreateImmutableAsync()).Pending;

        if (pending.Count == 0)
        {
            return 0;
        }

        var ran = 0;
        var done = new List<string>();
        var progress = new Dictionary<string, ResumableStepContext>(StringComparer.Ordinal);
        var seen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var step in steps.Where(step => pending.ContainsKey(step.Name)))
        {
            var context = new ResumableStepContext
            {
                Cursor = pending[step.Name].Cursor,
                Data = new Dictionary<string, string>(pending[step.Name].Data, StringComparer.Ordinal),
            };

            seen[step.Name] = [.. pending[step.Name].Data.Keys];

            try
            {
                if (await step.RunAsync(context, cancellationToken))
                {
                    done.Add(step.Name);
                }

                progress[step.Name] = context;
                ran++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The step runs again on the next pass, from the cursor it last kept.
                logger.LogError(exception, "The resumable step '{Step}' failed; it runs again on the next pass.", step.Name);
            }
        }

        if (progress.Count > 0)
        {
            var document = await _documents.GetOrCreateMutableAsync();

            foreach (var (name, context) in progress)
            {
                if (!document.Pending.TryGetValue(name, out var kept))
                {
                    continue;
                }

                // Data asked for while the step ran is kept for the next run, even when the step said it was done.
                foreach (var key in kept.Data.Keys.Where(key => !seen[name].Contains(key)))
                {
                    context.Data[key] = kept.Data[key];
                }

                if (done.Contains(name) && context.Data.Count == seen[name].Count(key => context.Data.ContainsKey(key)))
                {
                    document.Pending.Remove(name);

                    continue;
                }

                kept.Cursor = context.Cursor;
                kept.Data = context.Data;
            }

            await _documents.UpdateAsync(document);
        }

        return ran;
    }
}

/// <summary>
/// Runs a batch of each pending resumable step every minute, on one node at a time.
/// </summary>
[BackgroundTask(
    Title = "Resumable upgrade steps",
    Schedule = "* * * * *",
    Description = "Runs upgrade and change work too large for a request, a batch at a time, until it is done.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class ResumableStepsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(
            await serviceProvider.GetRequiredService<ResumableSteps>().RunPendingAsync(
                serviceProvider.GetServices<IResumableStep>(),
                serviceProvider.GetRequiredService<ILogger<ResumableStepsBackgroundTask>>(),
                cancellationToken),
            "steps"));
}

/// <summary>
/// Registers resumable steps.
/// </summary>
public static class ResumableStepsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a resumable step, the service that asks for steps and the background task that runs them.
    /// </summary>
    /// <typeparam name="TStep">The step.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddResumableStep<TStep>(this IServiceCollection services)
        where TStep : class, IResumableStep
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ResumableSteps>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IResumableStep, TStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ResumableStepsBackgroundTask>());

        return services;
    }
}
