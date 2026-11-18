using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// Which retrieved chunks reach a model. Exam content never does; a tutor gets only lessons of active enrollments in
/// courses whose learning assistant is on; another profile gets course items only when the person may view them.
/// </summary>
public sealed class LearningRetrievalPolicyTests
{
    private static readonly string[] _lessonKinds = new LearningAIOptions().TutorLessonKinds.ToArray();

    private static readonly LearningRetrievalCandidate _enrolledLesson = new()
    {
        Found = true,
        IsCourseItem = true,
        Kind = LearningConstants.CourseItemKinds.Page,
        AssistantEnabled = true,
        Access = CourseItemAccessReason.Enrolled,
        CanView = true,
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExamContent_NeverReachesAnyModel_EvenWhenThePersonMayViewIt(bool isTutor)
    {
        var question = new LearningRetrievalCandidate
        {
            Found = true,
            IsExamContent = true,
            IsCourseItem = true,
            Kind = "Quiz",
            AssistantEnabled = true,
            Access = CourseItemAccessReason.Enrolled,
            CanView = true,
        };

        Assert.False(LearningRetrievalPolicy.Keep(question, isTutor, _lessonKinds));
    }

    [Fact]
    public void Round10_4_AQuestionTypeAddedWithAddQuestionType_WithoutQuestionPart_IsExamContent_AndReachesNoProfile()
    {
        // Arrange: a module's own question type, registered as Assessments asks, whose content type has no QuestionPart.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.AssessmentsServiceCollectionExtensions.AddQuestionType<HotspotScorer>(services, "Hotspot", descriptor => descriptor.ContentType = "HotspotQuestion");
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var questionTypes = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Options.IOptions<CrestApps.OrchardCore.Learning.Assessments.QuestionTypeOptions>>(provider).Value;
        var question = new global::OrchardCore.ContentManagement.ContentItem { ContentItemId = "hotspot1", ContentType = "HotspotQuestion" };

        // Act
        var isExam = LearningRetrievalFilter.IsExamContent(question, questionTypes);
        var candidate = new LearningRetrievalCandidate { Found = true, IsExamContent = isExam, CanView = true };

        // Assert: exam content, so neither a tutor nor any other profile (such as NLWeb's) gets it.
        Assert.True(isExam);
        Assert.False(LearningRetrievalPolicy.Keep(candidate, isTutor: false, _lessonKinds));
        Assert.False(LearningRetrievalPolicy.Keep(candidate, isTutor: true, _lessonKinds));
    }

    private sealed class HotspotScorer : CrestApps.OrchardCore.Learning.Assessments.Services.IQuestionScorer
    {
        public string QuestionType => "Hotspot";

        public void Snapshot(global::OrchardCore.ContentManagement.ContentItem question, CrestApps.OrchardCore.Learning.Assessments.Models.AttemptItem item, Random random)
        {
        }

        public decimal? Score(CrestApps.OrchardCore.Learning.Assessments.Models.AttemptItem item)
            => null;
    }

    [Fact]
    public void Tutor_KeepsALessonOfAnActiveEnrollmentInACourseWithTheAssistantOn()
    {
        Assert.True(LearningRetrievalPolicy.Keep(_enrolledLesson, isTutor: true, _lessonKinds));
    }

    [Theory]
    [InlineData(CourseItemAccessReason.Staff)]
    [InlineData(CourseItemAccessReason.FreePreview)]
    [InlineData(CourseItemAccessReason.NotEnrolled)]
    [InlineData(CourseItemAccessReason.Pending)]
    [InlineData(CourseItemAccessReason.Suspended)]
    [InlineData(CourseItemAccessReason.AccessEnded)]
    [InlineData(CourseItemAccessReason.NotReleased)]
    [InlineData(CourseItemAccessReason.Anonymous)]
    [InlineData(CourseItemAccessReason.NotInCourse)]
    public void Tutor_DropsLessonsWithoutAnActiveEnrollmentWithinItsDatesAndRules(CourseItemAccessReason access)
    {
        var lesson = Copy(_enrolledLesson, access: access);

        Assert.False(LearningRetrievalPolicy.Keep(lesson, isTutor: true, _lessonKinds));
    }

    [Fact]
    public void Tutor_DropsLessonsOfACourseWhoseAssistantIsOff()
    {
        Assert.False(LearningRetrievalPolicy.Keep(Copy(_enrolledLesson, assistantEnabled: false), isTutor: true, _lessonKinds));
    }

    [Theory]
    [InlineData("Coursework")]
    [InlineData("LearningPackage")]
    [InlineData("ExternalTool")]
    [InlineData("Class")]
    [InlineData("Quiz")]
    public void Tutor_DropsCourseItemsThatAreNotLessons(string kind)
    {
        Assert.False(LearningRetrievalPolicy.Keep(Copy(_enrolledLesson, kind: kind), isTutor: true, _lessonKinds));
    }

    [Fact]
    public void Tutor_DropsContentThatIsNotACourseItemAndChunksWithoutAPublishedItem()
    {
        var page = new LearningRetrievalCandidate { Found = true, CanView = true };
        var document = new LearningRetrievalCandidate { Found = false };

        Assert.False(LearningRetrievalPolicy.Keep(page, isTutor: true, _lessonKinds));
        Assert.False(LearningRetrievalPolicy.Keep(document, isTutor: true, _lessonKinds));
    }

    [Fact]
    public void OtherProfiles_KeepCourseItemsOnlyWhenThePersonMayViewThem()
    {
        Assert.True(LearningRetrievalPolicy.Keep(_enrolledLesson, isTutor: false, _lessonKinds));
        Assert.False(LearningRetrievalPolicy.Keep(Copy(_enrolledLesson, canView: false), isTutor: false, _lessonKinds));
    }

    [Fact]
    public void OtherProfiles_KeepEverythingElseAsBefore()
    {
        var page = new LearningRetrievalCandidate { Found = true, CanView = false };
        var document = new LearningRetrievalCandidate { Found = false };

        Assert.True(LearningRetrievalPolicy.Keep(page, isTutor: false, _lessonKinds));
        Assert.True(LearningRetrievalPolicy.Keep(document, isTutor: false, _lessonKinds));
    }

    private static LearningRetrievalCandidate Copy(
        LearningRetrievalCandidate source,
        CourseItemAccessReason? access = null,
        bool? assistantEnabled = null,
        string kind = null,
        bool? canView = null)
        => new()
        {
            Found = source.Found,
            IsExamContent = source.IsExamContent,
            IsCourseItem = source.IsCourseItem,
            Kind = kind ?? source.Kind,
            AssistantEnabled = assistantEnabled ?? source.AssistantEnabled,
            Access = access ?? source.Access,
            CanView = canView ?? source.CanView,
        };
}
