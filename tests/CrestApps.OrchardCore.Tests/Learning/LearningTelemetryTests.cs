using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Telemetry;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Learning measures its own operations on the <c>CrestApps.OrchardCore.Learning</c> meter: enrollments by source,
/// status changes, completed items by who completed them, completed courses, and the upkeep run. No user, course or
/// enrollment id is a metric tag.
/// </summary>
public sealed class LearningTelemetryTests
{
    [Fact]
    public async Task EnrollingAndCompleting_RecordsMeasurements_WithBoundedTagsOnly()
    {
        // Arrange
        var measurements = new ConcurrentBag<(string Name, long Value, Dictionary<string, object> Tags)>();
        var durations = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LearningTelemetry.SourceName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => durations.Add(instrument.Name));
        listener.Start();

        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("telemetry-course");
        courses.AddItem("telemetry-course", "telemetry-lesson");

        // Act
        var enrollment = (await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "telemetry-learner",
            CourseId = "telemetry-course",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "telemetry",
        }))).Enrollment;
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(enrollment, "telemetry-lesson", 0));
        await harness.RunAsync(services => services.GetRequiredService<LearningUpkeep>().RunAsync());
        listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(measurements, m => m.Name == "crestapps.learning.enrollments" && Equals(m.Tags["source"], LearningConstants.EnrollmentSources.Manual));
        Assert.Contains(measurements, m => m.Name == "crestapps.learning.items.completed" && Equals(m.Tags["completed_by"], "learner"));
        Assert.Contains(measurements, m => m.Name == "crestapps.learning.courses.completed");
        Assert.Contains(measurements, m => m.Name == "crestapps.learning.enrollment.status_changes" && Equals(m.Tags["to"], nameof(EnrollmentStatus.Completed)));
        Assert.Contains("crestapps.learning.upkeep.duration", durations);

        var forbidden = new[] { "user", "user.id", "course", "course.id", "enrollment", "enrollment.id" };
        Assert.DoesNotContain(measurements, m => m.Tags.Keys.Any(key => forbidden.Contains(key)));
    }
}
