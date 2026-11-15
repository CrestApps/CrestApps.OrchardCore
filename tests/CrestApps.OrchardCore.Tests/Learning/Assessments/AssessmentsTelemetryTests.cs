using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Learning.Core.Telemetry;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Quiz attempts are counted on the Learning meter with bounded tags only.
/// </summary>
public sealed class AssessmentsTelemetryTests
{
    [Fact]
    public async Task StartingAndSubmittingAnAttempt_IsCounted_WithBoundedTags()
    {
        // Arrange
        var measurements = new ConcurrentBag<(string Name, Dictionary<string, object> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LearningTelemetry.SourceName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        listener.Start();
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("telemetry-course");
        h.Questions.AddChoice("tq1", "a");
        h.AddQuiz("telemetry-course", "telemetry-quiz", quiz => quiz.QuestionIds = ["tq1"]);
        await h.EnrollAsync("telemetry-learner", "telemetry-course");

        // Act
        var attempt = (await h.StartAsync("telemetry-learner", "telemetry-quiz")).Attempt;
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "telemetry-learner"));

        // Assert
        Assert.Contains(measurements, entry => entry.Name == "crestapps.learning.attempts.started");
        Assert.Contains(measurements, entry => entry.Name == "crestapps.learning.attempts.submitted" && Equals(entry.Tags["by"], "learner"));
        Assert.Contains(measurements, entry => entry.Name == "crestapps.learning.attempts.graded" && Equals(entry.Tags["by"], "automatic"));
        Assert.DoesNotContain(measurements, entry => entry.Tags.Values.Any(value => Equals(value, attempt.ItemId) || Equals(value, "telemetry-learner")));
    }
}
