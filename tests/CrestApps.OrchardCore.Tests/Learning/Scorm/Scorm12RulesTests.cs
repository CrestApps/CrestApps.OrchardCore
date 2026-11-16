using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class Scorm12RulesTests
{
    [Fact]
    public void FirstSession_IsAbInitio()
    {
        var cmi = new Scorm12Cmi { Entry = "resume", Exit = "suspend", SessionTime = "00:01:00" };

        Scorm12Rules.BeginSession(cmi, firstSession: true);

        Assert.Equal("ab-initio", cmi.Entry);
        Assert.Equal(string.Empty, cmi.Exit);
        Assert.Equal(string.Empty, cmi.SessionTime);
    }

    [Theory]
    [InlineData("suspend", "resume")]
    [InlineData("logout", "")]
    [InlineData("time-out", "")]
    [InlineData("", "")]
    public void LaterSession_ResumesOnlyAfterSuspend(string exit, string entry)
    {
        var cmi = new Scorm12Cmi { Exit = exit };

        Scorm12Rules.BeginSession(cmi, firstSession: false);

        Assert.Equal(entry, cmi.Entry);
        Assert.Equal(string.Empty, cmi.Exit);
    }

    [Theory]
    [InlineData("not attempted", "80", "passed")]
    [InlineData("completed", "79.5", "failed")]
    [InlineData("browsed", "95", "passed")]
    [InlineData("not attempted", "10", "failed")]
    public void MasteryScore_DecidesWhenTheContentDidNotPassOrFail(string status, string raw, string expected)
    {
        var cmi = new Scorm12Cmi { LessonStatus = status, ScoreRaw = raw, MasteryScore = "80" };

        Assert.True(Scorm12Rules.ApplyMasteryScore(cmi));
        Assert.Equal(expected, cmi.LessonStatus);
    }

    [Theory]
    [InlineData("passed", "10")]
    [InlineData("failed", "99")]
    [InlineData("incomplete", "90")]
    public void MasteryScore_LeavesTheContentsOwnPassOrFail_AndWorkItCallsIncomplete(string status, string raw)
    {
        var cmi = new Scorm12Cmi { LessonStatus = status, ScoreRaw = raw, MasteryScore = "80" };

        Assert.False(Scorm12Rules.ApplyMasteryScore(cmi));
        Assert.Equal(status, cmi.LessonStatus);
    }

    [Theory]
    [InlineData("", "90", "credit")]
    [InlineData("80", "", "credit")]
    [InlineData("80", "90", "no-credit")]
    public void MasteryScore_NeedsAMasteryScoreARawScoreAndCredit(string mastery, string raw, string credit)
    {
        var cmi = new Scorm12Cmi { LessonStatus = "completed", ScoreRaw = raw, MasteryScore = mastery, Credit = credit };

        Assert.False(Scorm12Rules.ApplyMasteryScore(cmi));
        Assert.Equal("completed", cmi.LessonStatus);
    }

    [Fact]
    public void Finish_AddsTheSessionTimeOnce()
    {
        var cmi = new Scorm12Cmi { TotalTime = "0001:59:30.50", SessionTime = "00:00:45.75", LessonStatus = "incomplete" };

        Scorm12Rules.FinishSession(cmi);

        Assert.Equal("0002:00:16.25", cmi.TotalTime);
        Assert.Equal(string.Empty, cmi.SessionTime);

        Scorm12Rules.FinishSession(cmi);

        Assert.Equal("0002:00:16.25", cmi.TotalTime);
    }

    [Fact]
    public void Finish_CompletesAStatusTheContentNeverSet()
    {
        var cmi = new Scorm12Cmi();

        Scorm12Rules.FinishSession(cmi);

        Assert.Equal("completed", cmi.LessonStatus);
    }

    [Fact]
    public void Finish_WithAScoreAndNoStatus_UsesTheMasteryScore()
    {
        var cmi = new Scorm12Cmi { ScoreRaw = "60", MasteryScore = "70" };

        Scorm12Rules.FinishSession(cmi);

        Assert.Equal("failed", cmi.LessonStatus);
    }

    [Fact]
    public void Finish_KeepsTheExitForTheNextEntry()
    {
        var cmi = new Scorm12Cmi { Exit = "suspend", LessonStatus = "incomplete" };

        Scorm12Rules.FinishSession(cmi);
        Scorm12Rules.BeginSession(cmi, firstSession: false);

        Assert.Equal("resume", cmi.Entry);
    }

    [Theory]
    [InlineData("0000:00:00.00", "00:00:01", "0000:00:01.00")]
    [InlineData("0000:59:59.99", "00:00:00.01", "0001:00:00.00")]
    [InlineData("9999:59:59.00", "01:00:00", "9999:59:59.99")]
    [InlineData("0000:00:00.00", "not a time", "0000:00:00.00")]
    [InlineData("", "00:10:00", "0000:10:00.00")]
    public void AddTimespan_FormatsAsHHHHMMSSss(string total, string add, string expected)
    {
        Assert.Equal(expected, Scorm12Rules.AddTimespan(total, add));
    }

    [Theory]
    [InlineData("00:01:02", 62)]
    [InlineData("0001:00:00.5", 3600.5)]
    public void TryParseTimespan_ReadsCmiTimespans(string value, double seconds)
    {
        Assert.True(Scorm12Rules.TryParseTimespan(value, out var span));
        Assert.Equal(seconds, span.TotalSeconds, 3);
    }
}
