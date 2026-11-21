using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Records the events the scoring services raise, by running them against a recording observer at once.
/// </summary>
internal sealed class ScoringDispatcher : ISurveyScoringEventDispatcher
{
    public List<string> Events { get; } = [];

    public void Dispatch(Func<ISurveyResponseObserver, Task> notify)
        => notify(new Recorder(Events)).GetAwaiter().GetResult();

    private sealed class Recorder : ISurveyResponseObserver
    {
        private readonly List<string> _events;

        public Recorder(List<string> events)
        {
            _events = events;
        }

        public Task GradedAsync(SurveyResponseEventContext context)
        {
            _events.Add("Graded");

            return Task.CompletedTask;
        }

        public Task ScoreChangedAsync(SurveyResponseEventContext context)
        {
            _events.Add("ScoreChanged");

            return Task.CompletedTask;
        }

        public Task OverrideGrantedAsync(SurveyOverride surveyOverride)
        {
            _events.Add("OverrideGranted");

            return Task.CompletedTask;
        }
    }
}
