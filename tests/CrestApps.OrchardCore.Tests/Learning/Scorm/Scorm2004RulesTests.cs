using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// What the LMS does with the SCORM 2004 data around a session: ISO 8601 durations, the total time of an attempt, and
/// whether a launch resumes the attempt or starts a new one.
/// </summary>
public sealed class Scorm2004RulesTests
{
    [Theory]
    [InlineData("PT0S", true)]
    [InlineData("PT1H30M", true)]
    [InlineData("PT2M30.5S", true)]
    [InlineData("PT0.25S", true)]
    [InlineData("P1DT2H", true)]
    [InlineData("P1Y2M3DT4H5M6.78S", true)]
    [InlineData("P3D", true)]
    [InlineData("P", false)]
    [InlineData("PT", false)]
    [InlineData("P1DT", false)]
    [InlineData("1H", false)]
    [InlineData("PT-1S", false)]
    [InlineData("P1H", false)]
    [InlineData("PT1.5M", false)]
    [InlineData("pt1h", false)]
    [InlineData("0000:10:00.00", false)]
    [InlineData("", false)]
    public void IsDuration_TakesIso8601Durations(string value, bool valid)
    {
        Assert.Equal(valid, Scorm2004Rules.IsDuration(value));
    }

    [Theory]
    [InlineData("PT0H0M0S", "PT2M30S", "PT0H2M30S")]
    [InlineData("PT1H59M59.5S", "PT0.5S", "PT2H0M0S")]
    [InlineData("PT0H0M0S", "P1DT1H", "PT25H0M0S")]
    [InlineData("PT0.25S", "PT0.25S", "PT0H0M0.5S")]
    [InlineData("PT10M", "PT0S", "PT0H10M0S")]
    [InlineData("PT0H0M0S", "not a duration", "PT0H0M0S")]
    [InlineData("", "PT1M", "PT0H1M0S")]
    public void AddDuration_SumsAsHoursMinutesAndSeconds(string total, string add, string expected)
    {
        Assert.Equal(expected, Scorm2004Rules.AddDuration(total, add));
    }

    [Fact]
    public void FinishSession_AddsTheSessionTimeToTheTotalOnce()
    {
        var cmi = new Scorm2004Cmi { TotalTime = "PT1H0M0S", SessionTime = "PT15M30S" };

        Scorm2004Rules.FinishSession(cmi);
        Scorm2004Rules.FinishSession(cmi);

        Assert.Equal("PT1H15M30S", cmi.TotalTime);
        Assert.Null(cmi.SessionTime);
    }

    [Fact]
    public void SessionTimes_AccumulateAcrossSuspendedSessionsOfOneAttempt()
    {
        var cmi = new Scorm2004Cmi();

        Assert.True(Scorm2004Rules.BeginSession(cmi, firstSession: true, []));
        cmi.SessionTime = "PT10M";
        cmi.Exit = "suspend";
        Scorm2004Rules.FinishSession(cmi);

        Assert.False(Scorm2004Rules.BeginSession(cmi, firstSession: false, []));
        cmi.SessionTime = "PT5M20S";
        Scorm2004Rules.FinishSession(cmi);

        Assert.Equal("PT0H15M20S", cmi.TotalTime);
    }

    [Fact]
    public void FirstSession_IsAnAbInitioAttempt_WithTheManifestObjectives()
    {
        var cmi = new Scorm2004Cmi { Entry = string.Empty };

        var newAttempt = Scorm2004Rules.BeginSession(cmi, firstSession: true, ["PRIMARY", "obj-2"]);

        Assert.True(newAttempt);
        Assert.Equal("ab-initio", cmi.Entry);
        Assert.Equal(["PRIMARY", "obj-2"], cmi.Objectives.Select(objective => objective.Id));
        Assert.All(cmi.Objectives, objective => Assert.Equal("unknown", objective.SuccessStatus));
    }

    [Fact]
    public void SuspendedAttempt_Resumes_WithItsData()
    {
        var cmi = Played("suspend");

        var newAttempt = Scorm2004Rules.BeginSession(cmi, firstSession: false, ["PRIMARY"]);

        Assert.False(newAttempt);
        Assert.Equal("resume", cmi.Entry);
        Assert.Equal("slide-7", cmi.Location);
        Assert.Equal("page=7", cmi.SuspendData);
        Assert.Equal("incomplete", cmi.CompletionStatus);
        Assert.Equal("0.4", cmi.ScoreScaled);
        Assert.Equal("PT0H20M0S", cmi.TotalTime);
        Assert.Single(cmi.Interactions);
        Assert.Equal(string.Empty, cmi.Exit);
        Assert.Null(cmi.SessionTime);
        Assert.Equal("_none_", cmi.NavRequest);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("")]
    [InlineData("logout")]
    [InlineData("time-out")]
    public void AttemptThatEnded_StartsANewAbInitioAttempt_WithTheDataReset(string exit)
    {
        var cmi = Played(exit);

        var newAttempt = Scorm2004Rules.BeginSession(cmi, firstSession: false, ["PRIMARY"]);

        Assert.True(newAttempt);
        Assert.Equal("ab-initio", cmi.Entry);
        Assert.Null(cmi.Location);
        Assert.Null(cmi.SuspendData);
        Assert.Equal("unknown", cmi.CompletionStatus);
        Assert.Equal("unknown", cmi.SuccessStatus);
        Assert.Null(cmi.ScoreScaled);
        Assert.Null(cmi.ScoreRaw);
        Assert.Null(cmi.ProgressMeasure);
        Assert.Equal("PT0H0M0S", cmi.TotalTime);
        Assert.Empty(cmi.Interactions);
        Assert.Empty(cmi.CommentsFromLearner);
        Assert.Equal(["PRIMARY"], cmi.Objectives.Select(objective => objective.Id));
        Assert.Equal("unknown", cmi.Objectives[0].SuccessStatus);
        Assert.Equal(string.Empty, cmi.Exit);
        Assert.Equal("_none_", cmi.NavRequest);

        // The learner's preferences and the edition are not attempt data.
        Assert.Equal("fr", cmi.Language);
        Assert.Equal("0.5", cmi.AudioLevel);
        Assert.Equal(3, cmi.Edition);
    }

    private static Scorm2004Cmi Played(string exit)
        => new()
        {
            Edition = 3,
            Location = "slide-7",
            SuspendData = "page=7",
            CompletionStatus = "incomplete",
            SuccessStatus = "failed",
            ScoreScaled = "0.4",
            ScoreRaw = "40",
            ProgressMeasure = "0.5",
            TotalTime = "PT0H20M0S",
            SessionTime = "PT5M",
            Exit = exit,
            NavRequest = "suspendAll",
            Language = "fr",
            AudioLevel = "0.5",
            Objectives = [new Scorm2004Objective { Id = "PRIMARY", SuccessStatus = "failed", ScoreScaled = "0.4" }],
            Interactions = [new Scorm2004Interaction { Id = "q1", Type = "true-false", LearnerResponse = "true" }],
            CommentsFromLearner = [new Scorm2004Comment { Comment = "hard" }],
        };
}
