using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Tells response observers in a deferred task of the shell scope, so they run after the change is committed and one
/// failing observer never undoes it.
/// </summary>
public sealed class DeferredSurveyScoringEventDispatcher : ISurveyScoringEventDispatcher
{
    /// <inheritdoc />
    public void Dispatch(Func<ISurveyResponseObserver, Task> notify)
    {
        ArgumentNullException.ThrowIfNull(notify);

        ShellScope.AddDeferredTask(async scope =>
        {
            var logger = scope.ServiceProvider.GetService<ILogger<DeferredSurveyScoringEventDispatcher>>();

            foreach (var observer in scope.ServiceProvider.GetServices<ISurveyResponseObserver>())
            {
                try
                {
                    await notify(observer);
                }
                catch (Exception exception)
                {
                    logger?.LogError(exception, "A survey response observer failed: {Observer}.", observer.GetType().Name);
                }
            }
        });
    }
}
