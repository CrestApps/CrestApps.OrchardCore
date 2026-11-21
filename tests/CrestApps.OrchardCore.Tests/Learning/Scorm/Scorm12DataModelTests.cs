using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The SCORM 1.2 run-time data model, element by element (RTE section 3.4), with the error codes of section 3.3.
/// </summary>
public sealed class Scorm12DataModelTests
{
    private const string NoError = Scorm12Errors.NoError;

    [Theory]
    [InlineData("cmi._version", "3.4")]
    [InlineData("cmi._children", "core,suspend_data,launch_data,comments,objectives,student_data,student_preference,interactions")]
    [InlineData("cmi.core._children", "student_id,student_name,lesson_location,credit,lesson_status,entry,score,total_time,lesson_mode,exit,session_time")]
    [InlineData("cmi.core.score._children", "raw,min,max")]
    [InlineData("cmi.objectives._children", "id,score,status")]
    [InlineData("cmi.student_data._children", "mastery_score,max_time_allowed,time_limit_action")]
    [InlineData("cmi.student_preference._children", "audio,language,speed,text")]
    [InlineData("cmi.interactions._children", "id,objectives,time,type,correct_responses,weighting,student_response,result,latency")]
    public void Children_AreListed(string element, string expected)
    {
        Assert.Equal(new Scorm12Read(expected, NoError), Scorm12DataModel.Get(new Scorm12Cmi(), element));
    }

    [Fact]
    public void ReadOnlyValues_AreRead()
    {
        var cmi = new Scorm12Cmi
        {
            StudentId = "u-42",
            StudentName = "Rivera, Ana",
            Credit = "credit",
            Entry = "resume",
            TotalTime = "0001:02:03.40",
            LessonMode = "normal",
            LaunchData = "level=2",
            CommentsFromLms = "Welcome",
            MasteryScore = "80",
            MaxTimeAllowed = "00:30:00",
            TimeLimitAction = "exit,message",
        };

        Assert.Equal("u-42", Get(cmi, "cmi.core.student_id"));
        Assert.Equal("Rivera, Ana", Get(cmi, "cmi.core.student_name"));
        Assert.Equal("credit", Get(cmi, "cmi.core.credit"));
        Assert.Equal("resume", Get(cmi, "cmi.core.entry"));
        Assert.Equal("0001:02:03.40", Get(cmi, "cmi.core.total_time"));
        Assert.Equal("normal", Get(cmi, "cmi.core.lesson_mode"));
        Assert.Equal("level=2", Get(cmi, "cmi.launch_data"));
        Assert.Equal("Welcome", Get(cmi, "cmi.comments_from_lms"));
        Assert.Equal("80", Get(cmi, "cmi.student_data.mastery_score"));
        Assert.Equal("00:30:00", Get(cmi, "cmi.student_data.max_time_allowed"));
        Assert.Equal("exit,message", Get(cmi, "cmi.student_data.time_limit_action"));
        Assert.Equal("not attempted", Get(new Scorm12Cmi(), "cmi.core.lesson_status"));
        Assert.Equal("0", Get(new Scorm12Cmi(), "cmi.objectives._count"));
        Assert.Equal("0", Get(new Scorm12Cmi(), "cmi.interactions._count"));
    }

    [Theory]
    [InlineData("cmi.core.student_id")]
    [InlineData("cmi.core.student_name")]
    [InlineData("cmi.core.credit")]
    [InlineData("cmi.core.entry")]
    [InlineData("cmi.core.total_time")]
    [InlineData("cmi.core.lesson_mode")]
    [InlineData("cmi.launch_data")]
    [InlineData("cmi.comments_from_lms")]
    [InlineData("cmi.student_data.mastery_score")]
    [InlineData("cmi.student_data.max_time_allowed")]
    [InlineData("cmi.student_data.time_limit_action")]
    public void ReadOnly_Set_Is403(string element)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(Scorm12Errors.ReadOnly, Scorm12DataModel.Set(cmi, element, "x"));
    }

    [Theory]
    [InlineData("cmi.core.exit")]
    [InlineData("cmi.core.session_time")]
    public void WriteOnly_Get_Is404(string element)
    {
        Assert.Equal(new Scorm12Read(string.Empty, Scorm12Errors.WriteOnly), Scorm12DataModel.Get(new Scorm12Cmi(), element));
    }

    [Theory]
    [InlineData("cmi._version")]
    [InlineData("cmi._children")]
    [InlineData("cmi.core._children")]
    [InlineData("cmi.core.score._children")]
    [InlineData("cmi.objectives._children")]
    [InlineData("cmi.objectives._count")]
    [InlineData("cmi.student_data._children")]
    [InlineData("cmi.student_preference._children")]
    [InlineData("cmi.interactions._children")]
    [InlineData("cmi.interactions._count")]
    public void Keyword_Set_Is402(string element)
    {
        Assert.Equal(Scorm12Errors.InvalidSetKeyword, Scorm12DataModel.Set(new Scorm12Cmi(), element, "1"));
    }

    [Theory]
    [InlineData("cmi.core.student_id._children")]
    [InlineData("cmi.core.lesson_status._children")]
    [InlineData("cmi.suspend_data._children")]
    public void Children_OfAnElementWithoutChildren_Is202(string element)
    {
        Assert.Equal(Scorm12Errors.CannotHaveChildren, Scorm12DataModel.Get(new Scorm12Cmi(), element).Error);
    }

    [Theory]
    [InlineData("cmi.core._count")]
    [InlineData("cmi.core.score._count")]
    [InlineData("cmi.core.lesson_status._count")]
    [InlineData("cmi.student_preference._count")]
    public void Count_OfAnElementThatIsNotAnArray_Is203(string element)
    {
        Assert.Equal(Scorm12Errors.NotAnArray, Scorm12DataModel.Get(new Scorm12Cmi(), element).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cmi")]
    [InlineData("cmi.core.unknown")]
    [InlineData("cmi.foo._children")]
    [InlineData("cmi.foo._count")]
    [InlineData("adl.nav.request")]
    [InlineData("cmi.completion_status")]
    public void UnknownElement_Is201OnGetAndSet(string element)
    {
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Get(new Scorm12Cmi(), element).Error);
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(new Scorm12Cmi(), element, "x"));
    }

    [Theory]
    [InlineData("passed")]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("incomplete")]
    [InlineData("browsed")]
    public void LessonStatus_TakesItsVocabulary(string value)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.core.lesson_status", value));
        Assert.Equal(value, Get(cmi, "cmi.core.lesson_status"));
    }

    [Theory]
    [InlineData("not attempted")]
    [InlineData("Passed")]
    [InlineData("complete")]
    [InlineData("")]
    [InlineData("unknown")]
    public void LessonStatus_RefusesOtherValues_With405(string value)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.core.lesson_status", value));
        Assert.Equal("not attempted", cmi.LessonStatus);
    }

    [Theory]
    [InlineData("time-out", true)]
    [InlineData("suspend", true)]
    [InlineData("logout", true)]
    [InlineData("", true)]
    [InlineData("normal", false)]
    [InlineData("Suspend", false)]
    public void Exit_TakesItsVocabulary(string value, bool valid)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.core.exit", value));
        Assert.Equal(valid ? value : string.Empty, cmi.Exit);
    }

    [Theory]
    [InlineData("cmi.core.score.raw")]
    [InlineData("cmi.core.score.min")]
    [InlineData("cmi.core.score.max")]
    [InlineData("cmi.objectives.0.score.raw")]
    [InlineData("cmi.objectives.0.score.min")]
    [InlineData("cmi.objectives.0.score.max")]
    public void Scores_AreCmiDecimalsFrom0To100_OrBlank(string element)
    {
        foreach (var valid in new[] { "0", "100", "85", "85.5", "0.25", "", "99.999" })
        {
            var cmi = new Scorm12Cmi();

            Assert.Equal(NoError, Scorm12DataModel.Set(cmi, element, valid));
            Assert.Equal(valid, Get(cmi, element));
        }

        foreach (var invalid in new[] { "-1", "100.01", "101", "abc", "8,5", "1e2", " 5", "NaN", "+5" })
        {
            Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), element, invalid));
        }
    }

    [Fact]
    public void LessonLocation_IsCmiString255()
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.core.lesson_location", new string('p', 255)));
        Assert.Equal(new string('p', 255), Get(cmi, "cmi.core.lesson_location"));
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.core.lesson_location", new string('p', 256)));
        Assert.Equal(new string('p', 255), cmi.LessonLocation);
    }

    [Theory]
    [InlineData("cmi.suspend_data")]
    [InlineData("cmi.comments")]
    public void SuspendDataAndComments_AreCmiString4096(string element)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, element, new string('s', 4096)));
        Assert.Equal(new string('s', 4096), Get(cmi, element));
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, element, new string('s', 4097)));
    }

    [Theory]
    [InlineData("00:00:00", true)]
    [InlineData("0000:00:00.00", true)]
    [InlineData("01:30:15", true)]
    [InlineData("9999:59:59.99", true)]
    [InlineData("12:05:07.5", true)]
    [InlineData("1:30:00", false)]
    [InlineData("00:60:00", false)]
    [InlineData("00:00:60", false)]
    [InlineData("00:00:00.123", false)]
    [InlineData("10000:00:00", false)]
    [InlineData("PT1H30M", false)]
    [InlineData("", false)]
    public void SessionTime_IsCmiTimespan(string value, bool valid)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.core.session_time", value));
        Assert.Equal(valid ? value : string.Empty, cmi.SessionTime);
    }

    [Theory]
    [InlineData("cmi.student_preference.audio", "-1", true)]
    [InlineData("cmi.student_preference.audio", "100", true)]
    [InlineData("cmi.student_preference.audio", "0", true)]
    [InlineData("cmi.student_preference.audio", "-2", false)]
    [InlineData("cmi.student_preference.audio", "101", false)]
    [InlineData("cmi.student_preference.audio", "1.5", false)]
    [InlineData("cmi.student_preference.speed", "-100", true)]
    [InlineData("cmi.student_preference.speed", "100", true)]
    [InlineData("cmi.student_preference.speed", "-101", false)]
    [InlineData("cmi.student_preference.text", "-1", true)]
    [InlineData("cmi.student_preference.text", "1", true)]
    [InlineData("cmi.student_preference.text", "2", false)]
    [InlineData("cmi.student_preference.language", "fr-CA", true)]
    public void StudentPreferences_HaveTheirRanges(string element, string value, bool valid)
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, element, value));

        if (valid)
        {
            Assert.Equal(value, Get(cmi, element));
        }
    }

    [Fact]
    public void StudentPreferenceLanguage_IsCmiString255()
    {
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.student_preference.language", new string('l', 256)));
    }

    [Fact]
    public void Objectives_GrowOnlyAtTheCount()
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.objectives.1.id", "obj-2"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.objectives.0.id", "obj-1"));
        Assert.Equal("1", Get(cmi, "cmi.objectives._count"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.objectives.0.status", "passed"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.objectives.1.score.raw", "40"));
        Assert.Equal("2", Get(cmi, "cmi.objectives._count"));
        Assert.Equal("obj-1", Get(cmi, "cmi.objectives.0.id"));
        Assert.Equal("passed", Get(cmi, "cmi.objectives.0.status"));
        Assert.Equal("40", Get(cmi, "cmi.objectives.1.score.raw"));
        Assert.Equal("raw,min,max", Get(cmi, "cmi.objectives.0.score._children"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Get(cmi, "cmi.objectives.2.id").Error);
        Assert.Equal(Scorm12Errors.InvalidSetKeyword, Scorm12DataModel.Set(cmi, "cmi.objectives.0.score._children", "x"));
    }

    [Theory]
    [InlineData("passed", true)]
    [InlineData("completed", true)]
    [InlineData("failed", true)]
    [InlineData("incomplete", true)]
    [InlineData("browsed", true)]
    [InlineData("not attempted", true)]
    [InlineData("done", false)]
    public void ObjectiveStatus_TakesTheVocabularyWithNotAttempted(string value, bool valid)
    {
        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.objectives.0.status", value));
    }

    [Theory]
    [InlineData("obj-1", true)]
    [InlineData("Q_1.a", true)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void ObjectiveId_IsCmiIdentifier(string value, bool valid)
    {
        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.objectives.0.id", value));
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.objectives.0.id", new string('i', 256)));
    }

    [Fact]
    public void RefusedValue_DoesNotAddAnArrayEntry()
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.objectives.0.status", "bad"));
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(cmi, "cmi.interactions.0.type", "essay"));
        Assert.Equal("0", Get(cmi, "cmi.objectives._count"));
        Assert.Equal("0", Get(cmi, "cmi.interactions._count"));
    }

    [Fact]
    public void Interactions_AreWriteOnlyWithReadableCounts()
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.id", "q1"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.type", "choice"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.time", "14:05:30"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.objectives.0.id", "obj-1"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.correct_responses.0.pattern", "b"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.weighting", "1.5"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.student_response", "a"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.result", "wrong"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.latency", "00:00:12.5"));

        Assert.Equal("1", Get(cmi, "cmi.interactions._count"));
        Assert.Equal("1", Get(cmi, "cmi.interactions.0.objectives._count"));
        Assert.Equal("1", Get(cmi, "cmi.interactions.0.correct_responses._count"));

        foreach (var element in new[] { "id", "type", "time", "objectives.0.id", "correct_responses.0.pattern", "weighting", "student_response", "result", "latency" })
        {
            Assert.Equal(Scorm12Errors.WriteOnly, Scorm12DataModel.Get(cmi, "cmi.interactions.0." + element).Error);
        }

        var interaction = Assert.Single(cmi.Interactions);

        Assert.Equal("q1", interaction.Id);
        Assert.Equal(["obj-1"], interaction.ObjectiveIds);
        Assert.Equal(["b"], interaction.CorrectResponses);
        Assert.Equal("wrong", interaction.Result);
        Assert.Equal(Scorm12Errors.InvalidSetKeyword, Scorm12DataModel.Set(cmi, "cmi.interactions.0.objectives._count", "3"));
    }

    [Fact]
    public void InteractionArrays_GrowOnlyAtTheCount()
    {
        var cmi = new Scorm12Cmi();

        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.1.id", "q2"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.0.objectives.1.id", "o"));
        Assert.Equal(NoError, Scorm12DataModel.Set(cmi, "cmi.interactions.0.objectives.0.id", "o"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.0.correct_responses.1.pattern", "x"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Get(cmi, "cmi.interactions.3.objectives._count").Error);
    }

    [Theory]
    [InlineData("type", "true-false", true)]
    [InlineData("type", "choice", true)]
    [InlineData("type", "fill-in", true)]
    [InlineData("type", "matching", true)]
    [InlineData("type", "performance", true)]
    [InlineData("type", "sequencing", true)]
    [InlineData("type", "likert", true)]
    [InlineData("type", "numeric", true)]
    [InlineData("type", "long-fill-in", false)]
    [InlineData("result", "correct", true)]
    [InlineData("result", "wrong", true)]
    [InlineData("result", "unanticipated", true)]
    [InlineData("result", "neutral", true)]
    [InlineData("result", "0.75", true)]
    [InlineData("result", "incorrect", false)]
    [InlineData("time", "23:59:59", true)]
    [InlineData("time", "23:59:59.99", true)]
    [InlineData("time", "24:00:00", false)]
    [InlineData("time", "7:00:00", false)]
    [InlineData("latency", "0000:01:30", true)]
    [InlineData("latency", "1:30", false)]
    [InlineData("weighting", "-2.5", true)]
    [InlineData("weighting", "heavy", false)]
    [InlineData("student_response", "a,b", true)]
    [InlineData("id", "q 1", false)]
    public void InteractionValues_AreChecked(string element, string value, bool valid)
    {
        Assert.Equal(valid ? NoError : Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.interactions.0." + element, value));
    }

    [Fact]
    public void InteractionResponse_IsAtMost255Characters()
    {
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.interactions.0.student_response", new string('r', 256)));
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.interactions.0.correct_responses.0.pattern", new string('r', 256)));
    }

    [Fact]
    public void NullValue_IsRefused()
    {
        Assert.Equal(Scorm12Errors.IncorrectDataType, Scorm12DataModel.Set(new Scorm12Cmi(), "cmi.core.lesson_status", null));
    }

    private static string Get(Scorm12Cmi cmi, string element)
    {
        var read = Scorm12DataModel.Get(cmi, element);

        Assert.Equal(NoError, read.Error);

        return read.Value;
    }
}
