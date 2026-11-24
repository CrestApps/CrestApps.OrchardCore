using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The SCORM 2004 run-time data model, element by element (RTE section 4.2), with the error codes of section 3.1.7.
/// </summary>
public sealed class Scorm2004DataModelTests
{
    private const string NoError = Scorm2004Errors.NoError;

    [Theory]
    [InlineData("cmi._version", "1.0")]
    [InlineData("cmi.comments_from_learner._children", "comment,location,timestamp")]
    [InlineData("cmi.comments_from_lms._children", "comment,location,timestamp")]
    [InlineData("cmi.interactions._children", "id,type,objectives,timestamp,correct_responses,weighting,learner_response,result,latency,description")]
    [InlineData("cmi.learner_preference._children", "audio_level,language,delivery_speed,audio_captioning")]
    [InlineData("cmi.objectives._children", "id,score,success_status,completion_status,progress_measure,description")]
    [InlineData("cmi.score._children", "scaled,raw,min,max")]
    [InlineData("cmi.comments_from_learner._count", "0")]
    [InlineData("cmi.comments_from_lms._count", "0")]
    [InlineData("cmi.interactions._count", "0")]
    [InlineData("cmi.objectives._count", "0")]
    [InlineData("adl.data._children", "id,store")]
    [InlineData("adl.data._count", "0")]
    public void Keywords_AreAnswered(string element, string expected)
    {
        Assert.Equal(new Scorm2004Read(expected, NoError), Scorm2004DataModel.Get(new Scorm2004Cmi(), element));
    }

    [Fact]
    public void NewData_HasTheDefaultsOfTheRte()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal("unknown", Get(cmi, "cmi.completion_status"));
        Assert.Equal("unknown", Get(cmi, "cmi.success_status"));
        Assert.Equal("ab-initio", Get(cmi, "cmi.entry"));
        Assert.Equal("credit", Get(cmi, "cmi.credit"));
        Assert.Equal("normal", Get(cmi, "cmi.mode"));
        Assert.Equal("PT0H0M0S", Get(cmi, "cmi.total_time"));
        Assert.Equal("continue,no message", Get(cmi, "cmi.time_limit_action"));
        Assert.Equal("1", Get(cmi, "cmi.learner_preference.audio_level"));
        Assert.Equal(string.Empty, Get(cmi, "cmi.learner_preference.language"));
        Assert.Equal("1", Get(cmi, "cmi.learner_preference.delivery_speed"));
        Assert.Equal("0", Get(cmi, "cmi.learner_preference.audio_captioning"));
        Assert.Equal("_none_", Get(cmi, "adl.nav.request"));
    }

    [Fact]
    public void ReadOnlyValues_AreRead()
    {
        var cmi = new Scorm2004Cmi
        {
            LearnerId = "u-42",
            LearnerName = "Rivera, Ana",
            LaunchData = "chapter=1",
            CompletionThreshold = "0.8",
            ScaledPassingScore = "0.6",
            MaxTimeAllowed = "PT30M",
            TimeLimitAction = "exit,message",
            Credit = "no-credit",
            Mode = "review",
            Entry = "resume",
            TotalTime = "PT1H2M3S",
            CommentsFromLms = [new Scorm2004Comment { Comment = "{lang=en}Welcome", Location = "intro", Timestamp = "2026-11-16T10:00:00Z" }],
        };

        Assert.Equal("u-42", Get(cmi, "cmi.learner_id"));
        Assert.Equal("Rivera, Ana", Get(cmi, "cmi.learner_name"));
        Assert.Equal("chapter=1", Get(cmi, "cmi.launch_data"));
        Assert.Equal("0.8", Get(cmi, "cmi.completion_threshold"));
        Assert.Equal("0.6", Get(cmi, "cmi.scaled_passing_score"));
        Assert.Equal("PT30M", Get(cmi, "cmi.max_time_allowed"));
        Assert.Equal("exit,message", Get(cmi, "cmi.time_limit_action"));
        Assert.Equal("no-credit", Get(cmi, "cmi.credit"));
        Assert.Equal("review", Get(cmi, "cmi.mode"));
        Assert.Equal("resume", Get(cmi, "cmi.entry"));
        Assert.Equal("PT1H2M3S", Get(cmi, "cmi.total_time"));
        Assert.Equal("1", Get(cmi, "cmi.comments_from_lms._count"));
        Assert.Equal("{lang=en}Welcome", Get(cmi, "cmi.comments_from_lms.0.comment"));
        Assert.Equal("intro", Get(cmi, "cmi.comments_from_lms.0.location"));
        Assert.Equal("2026-11-16T10:00:00Z", Get(cmi, "cmi.comments_from_lms.0.timestamp"));
    }

    [Theory]
    [InlineData("cmi.location")]
    [InlineData("cmi.suspend_data")]
    [InlineData("cmi.launch_data")]
    [InlineData("cmi.score.scaled")]
    [InlineData("cmi.score.raw")]
    [InlineData("cmi.score.min")]
    [InlineData("cmi.score.max")]
    [InlineData("cmi.progress_measure")]
    [InlineData("cmi.completion_threshold")]
    [InlineData("cmi.scaled_passing_score")]
    [InlineData("cmi.max_time_allowed")]
    public void ElementNeverSet_IsNotInitialized_403(string element)
    {
        Assert.Equal(new Scorm2004Read(string.Empty, Scorm2004Errors.ValueNotInitialized), Scorm2004DataModel.Get(new Scorm2004Cmi(), element));
    }

    [Theory]
    [InlineData("cmi._version")]
    [InlineData("cmi.learner_id")]
    [InlineData("cmi.learner_name")]
    [InlineData("cmi.credit")]
    [InlineData("cmi.entry")]
    [InlineData("cmi.mode")]
    [InlineData("cmi.total_time")]
    [InlineData("cmi.launch_data")]
    [InlineData("cmi.completion_threshold")]
    [InlineData("cmi.scaled_passing_score")]
    [InlineData("cmi.max_time_allowed")]
    [InlineData("cmi.time_limit_action")]
    [InlineData("cmi.objectives._count")]
    [InlineData("cmi.objectives._children")]
    [InlineData("cmi.score._children")]
    [InlineData("cmi.interactions._count")]
    [InlineData("adl.nav.request_valid.continue")]
    [InlineData("adl.nav.request_valid.choice.{target=intro}")]
    public void ReadOnly_Set_Is404(string element)
    {
        Assert.Equal(Scorm2004Errors.ReadOnly, Scorm2004DataModel.Set(new Scorm2004Cmi(), element, "x"));
    }

    [Fact]
    public void CommentFromTheLms_Set_Is404()
    {
        var cmi = new Scorm2004Cmi { CommentsFromLms = [new Scorm2004Comment { Comment = "c" }] };

        Assert.Equal(Scorm2004Errors.ReadOnly, Scorm2004DataModel.Set(cmi, "cmi.comments_from_lms.0.comment", "x"));
    }

    [Theory]
    [InlineData("cmi.exit")]
    [InlineData("cmi.session_time")]
    public void WriteOnly_Get_Is405(string element)
    {
        Assert.Equal(Scorm2004Errors.WriteOnly, Scorm2004DataModel.Get(new Scorm2004Cmi(), element).Error);
    }

    [Theory]
    [InlineData("cmi.unknown")]
    [InlineData("cmi.core.lesson_status")]
    [InlineData("cmi.student_name")]
    [InlineData("adl.unknown")]
    [InlineData("cmi..location")]
    [InlineData("cmi.location.")]
    public void UndefinedElement_Is401(string element)
    {
        Assert.Equal(Scorm2004Errors.UndefinedDataModelElement, Scorm2004DataModel.Get(new Scorm2004Cmi(), element).Error);
        Assert.Equal(Scorm2004Errors.UndefinedDataModelElement, Scorm2004DataModel.Set(new Scorm2004Cmi(), element, "x"));
    }

    [Theory]
    [InlineData("cmi.location._children")]
    [InlineData("cmi.score._count")]
    [InlineData("cmi.learner_preference._count")]
    [InlineData("cmi.completion_status._version")]
    [InlineData("cmi.objectives.0._children")]
    public void KeywordTheElementDoesNotHave_Get_Is301(string element)
    {
        var cmi = new Scorm2004Cmi { Objectives = [new Scorm2004Objective { Id = "o" }] };

        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, element).Error);
    }

    [Fact]
    public void EmptyElement_Get_Is301_AndSet_Is351()
    {
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(new Scorm2004Cmi(), string.Empty).Error);
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(new Scorm2004Cmi(), string.Empty, "x"));
    }

    [Theory]
    [InlineData("cmi.completion_status", "passed")]
    [InlineData("cmi.completion_status", "Completed")]
    [InlineData("cmi.success_status", "completed")]
    [InlineData("cmi.exit", "suspended")]
    [InlineData("cmi.learner_preference.audio_captioning", "2")]
    [InlineData("cmi.score.raw", "ten")]
    [InlineData("cmi.score.scaled", "")]
    [InlineData("cmi.progress_measure", "1e-1")]
    [InlineData("cmi.session_time", "00:10:00")]
    [InlineData("cmi.learner_preference.language", "english")]
    [InlineData("adl.nav.request", "jumpto")]
    [InlineData("adl.nav.request", "{target=}choice")]
    public void WrongTypeOrVocabulary_Is406(string element, string value)
    {
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(new Scorm2004Cmi(), element, value));
    }

    [Theory]
    [InlineData("cmi.score.scaled", "1.5")]
    [InlineData("cmi.score.scaled", "-1.0001")]
    [InlineData("cmi.progress_measure", "1.01")]
    [InlineData("cmi.progress_measure", "-0.1")]
    [InlineData("cmi.learner_preference.audio_level", "-1")]
    [InlineData("cmi.learner_preference.delivery_speed", "-0.5")]
    public void RealOutsideItsRange_Is407(string element, string value)
    {
        Assert.Equal(Scorm2004Errors.ValueOutOfRange, Scorm2004DataModel.Set(new Scorm2004Cmi(), element, value));
    }

    [Theory]
    [InlineData("cmi.location", "slide-3")]
    [InlineData("cmi.suspend_data", "page=4;answers=1,2")]
    [InlineData("cmi.score.scaled", "-1")]
    [InlineData("cmi.score.scaled", "0.85")]
    [InlineData("cmi.score.raw", "-12.5")]
    [InlineData("cmi.score.min", "0")]
    [InlineData("cmi.score.max", "200")]
    [InlineData("cmi.progress_measure", "1")]
    [InlineData("cmi.completion_status", "not attempted")]
    [InlineData("cmi.success_status", "failed")]
    [InlineData("cmi.learner_preference.audio_level", "2.5")]
    [InlineData("cmi.learner_preference.language", "en-US")]
    [InlineData("cmi.learner_preference.language", "")]
    [InlineData("cmi.learner_preference.delivery_speed", "0.75")]
    [InlineData("cmi.learner_preference.audio_captioning", "-1")]
    [InlineData("adl.nav.request", "continue")]
    [InlineData("adl.nav.request", "suspendAll")]
    [InlineData("adl.nav.request", "{target=intro}choice")]
    [InlineData("adl.nav.request", "{target=intro}jump")]
    public void ValidValue_IsWritten_AndReadBack(string element, string value)
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, element, value));
        Assert.Equal(new Scorm2004Read(value, NoError), Scorm2004DataModel.Get(cmi, element));
    }

    [Theory]
    [InlineData("time-out")]
    [InlineData("suspend")]
    [InlineData("logout")]
    [InlineData("normal")]
    [InlineData("")]
    public void Exit_TakesItsVocabulary(string value)
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.exit", value));
        Assert.Equal(value, cmi.Exit);
    }

    [Fact]
    public void SessionTime_IsAnIso8601Duration()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.session_time", "PT2M30.5S"));
        Assert.Equal("PT2M30.5S", cmi.SessionTime);
    }

    [Fact]
    public void JumpNavigationRequest_IsOnlyInTheFourthEdition()
    {
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(new Scorm2004Cmi { Edition = 3 }, "adl.nav.request", "{target=intro}jump"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(new Scorm2004Cmi { Edition = 3 }, "adl.nav.request", "{target=intro}choice"));
    }

    [Theory]
    [InlineData("adl.nav.request_valid.continue")]
    [InlineData("adl.nav.request_valid.previous")]
    [InlineData("adl.nav.request_valid.choice.{target=intro}")]
    [InlineData("adl.nav.request_valid.jump.{target=lesson.2}")]
    public void NavigationRequestValid_IsUnknown_WithoutSequencing(string element)
    {
        Assert.Equal(new Scorm2004Read("unknown", NoError), Scorm2004DataModel.Get(new Scorm2004Cmi(), element));
    }

    [Fact]
    public void NavigationRequestValidJump_IsNotInTheThirdEdition()
    {
        Assert.NotEqual(NoError, Scorm2004DataModel.Get(new Scorm2004Cmi { Edition = 3 }, "adl.nav.request_valid.jump.{target=x}").Error);
    }

    [Fact]
    public void Location_TakesAThousandCharacters()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.location", new string('l', 1000)));
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, "cmi.location", new string('l', 1001)));
    }

    [Theory]
    [InlineData(4, 64000)]
    [InlineData(3, 4000)]
    public void SuspendData_TakesTheLengthOfTheEdition(int edition, int length)
    {
        var cmi = new Scorm2004Cmi { Edition = edition };

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.suspend_data", new string('s', length)));
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, "cmi.suspend_data", new string('s', length + 1)));
    }

    [Theory]
    [InlineData("plain text", true)]
    [InlineData("{lang=fr}Bonjour", true)]
    [InlineData("{lang=en-GB}Colour", true)]
    [InlineData("{lang=}empty", false)]
    [InlineData("{lang=12}digits", false)]
    [InlineData("{lang=en open", false)]
    public void LocalizedString_ChecksItsLanguageDelimiter(string value, bool valid)
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(valid ? NoError : Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.comment", value));
    }

    [Fact]
    public void LocalizedString_LengthLeavesOutTheDelimiter()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.comment", "{lang=en}" + new string('c', 4000)));
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.comment", new string('c', 4001)));
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.location", new string('l', 251)));
    }

    [Fact]
    public void CommentsFromLearner_GrowAtTheirCount_ByAnyElement()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.location", "slide-2"));
        Assert.Equal("1", Get(cmi, "cmi.comments_from_learner._count"));
        Assert.Equal(Scorm2004Errors.ValueNotInitialized, Scorm2004DataModel.Get(cmi, "cmi.comments_from_learner.0.comment").Error);
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.0.timestamp", "2026-11-16T09:15:00"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.2.comment", "x"));
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, "cmi.comments_from_learner.1.comment").Error);
    }

    [Fact]
    public void Objective_IsAddedByItsId_Only()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(Scorm2004Errors.DependencyNotEstablished, Scorm2004DataModel.Set(cmi, "cmi.objectives.0.success_status", "passed"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.objectives.1.id", "obj-2"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.objectives.0.id", "urn:example:obj-1"));
        Assert.Equal("1", Get(cmi, "cmi.objectives._count"));
        Assert.Equal("unknown", Get(cmi, "cmi.objectives.0.success_status"));
        Assert.Equal("unknown", Get(cmi, "cmi.objectives.0.completion_status"));
        Assert.Equal(Scorm2004Errors.ValueNotInitialized, Scorm2004DataModel.Get(cmi, "cmi.objectives.0.score.scaled").Error);
        Assert.Equal(Scorm2004Errors.ValueNotInitialized, Scorm2004DataModel.Get(cmi, "cmi.objectives.0.progress_measure").Error);
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, "cmi.objectives.1.id").Error);
        Assert.Equal("scaled,raw,min,max", Get(cmi, "cmi.objectives.0.score._children"));
    }

    [Fact]
    public void ObjectiveIds_AreUnique_AndDoNotChange()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.objectives.0.id", "obj-1"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.objectives.1.id", "obj-2"));

        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.objectives.1.id", "obj-1"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.objectives.2.id", "obj-1"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.objectives.0.id", "obj-renamed"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.objectives.0.id", "obj-1"));
        Assert.Equal("2", Get(cmi, "cmi.objectives._count"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("urn::empty-namespace")]
    [InlineData("urn:x:")]
    public void ObjectiveId_ThatIsNotALongIdentifier_Is406(string id)
    {
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(new Scorm2004Cmi(), "cmi.objectives.0.id", id));
    }

    [Fact]
    public void ObjectiveId_TakesFourThousandCharacters()
    {
        Assert.Equal(NoError, Scorm2004DataModel.Set(new Scorm2004Cmi(), "cmi.objectives.0.id", new string('o', 4000)));
        Assert.Equal(Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(new Scorm2004Cmi(), "cmi.objectives.0.id", new string('o', 4001)));
    }

    [Theory]
    [InlineData("cmi.objectives.0.score.scaled", "2", Scorm2004Errors.ValueOutOfRange)]
    [InlineData("cmi.objectives.0.score.scaled", "-0.5", NoError)]
    [InlineData("cmi.objectives.0.score.raw", "abc", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.objectives.0.progress_measure", "-1", Scorm2004Errors.ValueOutOfRange)]
    [InlineData("cmi.objectives.0.progress_measure", "0.5", NoError)]
    [InlineData("cmi.objectives.0.success_status", "passed", NoError)]
    [InlineData("cmi.objectives.0.success_status", "incomplete", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.objectives.0.completion_status", "incomplete", NoError)]
    [InlineData("cmi.objectives.0.description", "{lang=en}Check the forks", NoError)]
    public void ObjectiveElements_AreChecked(string element, string value, string expected)
    {
        var cmi = new Scorm2004Cmi { Objectives = [new Scorm2004Objective { Id = "obj-1" }] };

        Assert.Equal(expected, Scorm2004DataModel.Set(cmi, element, value));
    }

    [Fact]
    public void Interaction_IsAddedByItsId_AndResponsesNeedItsType()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(Scorm2004Errors.DependencyNotEstablished, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.type", "choice"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.id", "q1"));
        Assert.Equal(Scorm2004Errors.ValueNotInitialized, Scorm2004DataModel.Get(cmi, "cmi.interactions.0.type").Error);
        Assert.Equal(Scorm2004Errors.DependencyNotEstablished, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.learner_response", "a"));
        Assert.Equal(Scorm2004Errors.DependencyNotEstablished, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.0.pattern", "a"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.type", "choice"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.learner_response", "a[,]c"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.0.pattern", "a[,]b"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.1.pattern", "c"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.3.pattern", "d"));
        Assert.Equal("2", Get(cmi, "cmi.interactions.0.correct_responses._count"));
        Assert.Equal("a[,]c", Get(cmi, "cmi.interactions.0.learner_response"));
        Assert.Equal("a[,]b", Get(cmi, "cmi.interactions.0.correct_responses.0.pattern"));
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, "cmi.interactions.0.correct_responses.2.pattern").Error);
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, "cmi.interactions.1.id").Error);
    }

    [Theory]
    [InlineData("true-false")]
    [InlineData("likert")]
    [InlineData("numeric")]
    [InlineData("other")]
    public void InteractionWithOneCorrectResponse_RefusesASecond_351(string type)
    {
        var value = type switch
        {
            "true-false" => "true",
            "likert" => "agree",
            "numeric" => "1[:]5",
            _ => "anything",
        };
        var cmi = new Scorm2004Cmi { Interactions = [new Scorm2004Interaction { Id = "q1", Type = type }] };

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.0.pattern", value));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.correct_responses.1.pattern", value));
    }

    [Fact]
    public void InteractionObjectiveIds_AreUniqueWithinTheInteraction()
    {
        var cmi = new Scorm2004Cmi { Interactions = [new Scorm2004Interaction { Id = "q1" }, new Scorm2004Interaction { Id = "q2" }] };

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.objectives.0.id", "obj-1"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.objectives.1.id", "obj-2"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.objectives.2.id", "obj-1"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.objectives.5.id", "obj-5"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.1.objectives.0.id", "obj-1"));
        Assert.Equal("2", Get(cmi, "cmi.interactions.0.objectives._count"));
        Assert.Equal("obj-2", Get(cmi, "cmi.interactions.0.objectives.1.id"));
    }

    [Theory]
    [InlineData("cmi.interactions.0.timestamp", "2026-11-16T10:30:00Z", NoError)]
    [InlineData("cmi.interactions.0.timestamp", "16/11/2026", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.interactions.0.latency", "PT12.5S", NoError)]
    [InlineData("cmi.interactions.0.latency", "12 seconds", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.interactions.0.weighting", "0.25", NoError)]
    [InlineData("cmi.interactions.0.weighting", "heavy", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.interactions.0.result", "incorrect", NoError)]
    [InlineData("cmi.interactions.0.result", "0.75", NoError)]
    [InlineData("cmi.interactions.0.result", "wrong", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.interactions.0.type", "essay", Scorm2004Errors.TypeMismatch)]
    [InlineData("cmi.interactions.0.type", "long-fill-in", NoError)]
    [InlineData("cmi.interactions.0.description", "{lang=en}Which fork height?", NoError)]
    public void InteractionElements_AreChecked(string element, string value, string expected)
    {
        var cmi = new Scorm2004Cmi { Interactions = [new Scorm2004Interaction { Id = "q1" }] };

        Assert.Equal(expected, Scorm2004DataModel.Set(cmi, element, value));
    }

    [Theory]
    [InlineData("true-false", "true", true, true)]
    [InlineData("true-false", "yes", false, false)]
    [InlineData("choice", "a[,]b", false, true)]
    [InlineData("choice", "a[,]a", true, false)]
    [InlineData("choice", "", false, true)]
    [InlineData("fill-in", "{case_matters=true}{lang=en}red[,]blue", true, true)]
    [InlineData("fill-in", "{order_matters=false}red", true, true)]
    [InlineData("fill-in", "{case_matters=maybe}red", true, false)]
    [InlineData("fill-in", "red[,]{lang=en}blue", false, true)]
    [InlineData("long-fill-in", "{case_matters=false}A long answer", true, true)]
    [InlineData("long-fill-in", "{lang=en}A long answer", false, true)]
    [InlineData("likert", "strongly_agree", false, true)]
    [InlineData("likert", "strongly agree", false, false)]
    [InlineData("matching", "1[.]a[,]2[.]b", true, true)]
    [InlineData("matching", "1a", false, false)]
    [InlineData("performance", "{order_matters=true}step1[.]5[,]step2[.]go", true, true)]
    [InlineData("performance", "[.]", false, false)]
    [InlineData("sequencing", "a[,]b[,]c", true, true)]
    [InlineData("sequencing", "", false, false)]
    [InlineData("numeric", "1[:]5", true, true)]
    [InlineData("numeric", "[:]5", true, true)]
    [InlineData("numeric", "5[:]1", true, false)]
    [InlineData("numeric", "3.5", false, true)]
    [InlineData("numeric", "three", false, false)]
    [InlineData("other", "anything at all", false, true)]
    public void Responses_FollowTheFormatOfTheInteractionType(string type, string value, bool pattern, bool valid)
    {
        var cmi = new Scorm2004Cmi { Interactions = [new Scorm2004Interaction { Id = "q1", Type = type }] };
        var element = pattern ? "cmi.interactions.0.correct_responses.0.pattern" : "cmi.interactions.0.learner_response";

        Assert.Equal(valid ? NoError : Scorm2004Errors.TypeMismatch, Scorm2004DataModel.Set(cmi, element, value));
    }

    [Theory]
    [InlineData("2026", true)]
    [InlineData("2026-11", true)]
    [InlineData("2026-11-16", true)]
    [InlineData("2026-11-16T10", true)]
    [InlineData("2026-11-16T10:30", true)]
    [InlineData("2026-11-16T10:30:00", true)]
    [InlineData("2026-11-16T10:30:00.5", true)]
    [InlineData("2026-11-16T10:30:00Z", true)]
    [InlineData("2026-11-16T10:30:00.25+02:00", true)]
    [InlineData("2026-11-16T10:30:00-05", true)]
    [InlineData("1970-01-01T00:00:00", true)]
    [InlineData("1969-12-31", false)]
    [InlineData("2039-01-01", false)]
    [InlineData("2026-13-01", false)]
    [InlineData("2026-02-30", false)]
    [InlineData("2026-11-16T24:00:00", false)]
    [InlineData("2026-11-16T10:60", false)]
    [InlineData("2026-11-16 10:30:00", false)]
    [InlineData("", false)]
    public void Timestamp_IsIso8601_From1970To2038(string value, bool valid)
    {
        Assert.Equal(valid, Scorm2004DataModel.IsTimestamp(value));
    }

    [Theory]
    [InlineData(null, null, "incomplete", "incomplete")]
    [InlineData(null, "0.2", "completed", "completed")]
    [InlineData("0.8", null, "completed", "unknown")]
    [InlineData("0.8", "0.8", "incomplete", "completed")]
    [InlineData("0.8", "0.95", "unknown", "completed")]
    [InlineData("0.8", "0.79", "completed", "incomplete")]
    [InlineData("0", "0", "unknown", "completed")]
    public void CompletionStatus_IsEvaluatedAgainstTheThreshold(string threshold, string progress, string set, string expected)
    {
        var cmi = new Scorm2004Cmi { CompletionThreshold = threshold, ProgressMeasure = progress, CompletionStatus = set };

        Assert.Equal(expected, Scorm2004DataModel.EvaluateCompletionStatus(cmi));
        Assert.Equal(expected, Get(cmi, "cmi.completion_status"));
    }

    [Theory]
    [InlineData(null, null, "passed", "passed")]
    [InlineData(null, "0.1", "passed", "passed")]
    [InlineData("0.6", null, "passed", "unknown")]
    [InlineData("0.6", "0.6", "failed", "passed")]
    [InlineData("0.6", "0.59", "passed", "failed")]
    [InlineData("-0.5", "-0.4", "unknown", "passed")]
    [InlineData("-0.5", "-1", "unknown", "failed")]
    public void SuccessStatus_IsEvaluatedAgainstTheScaledPassingScore(string passing, string scaled, string set, string expected)
    {
        var cmi = new Scorm2004Cmi { ScaledPassingScore = passing, ScoreScaled = scaled, SuccessStatus = set };

        Assert.Equal(expected, Scorm2004DataModel.EvaluateSuccessStatus(cmi));
        Assert.Equal(expected, Get(cmi, "cmi.success_status"));
    }

    [Fact]
    public void StatusWritten_IsKeptAsSet_AndReadEvaluated()
    {
        var cmi = new Scorm2004Cmi { CompletionThreshold = "0.5" };

        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.completion_status", "incomplete"));
        Assert.Equal(NoError, Scorm2004DataModel.Set(cmi, "cmi.progress_measure", "0.6"));

        Assert.Equal("incomplete", cmi.CompletionStatus);
        Assert.Equal("completed", Get(cmi, "cmi.completion_status"));
    }

    [Fact]
    public void SharedDataStores_AreNotDeclared_SoEveryEntryIsOutOfRange()
    {
        var cmi = new Scorm2004Cmi();

        Assert.Equal(Scorm2004Errors.GeneralGetFailure, Scorm2004DataModel.Get(cmi, "adl.data.0.id").Error);
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "adl.data.0.store", "x"));
    }

    private static string Get(Scorm2004Cmi cmi, string element)
    {
        var read = Scorm2004DataModel.Get(cmi, element);

        Assert.Equal(NoError, read.Error);

        return read.Value;
    }
}
