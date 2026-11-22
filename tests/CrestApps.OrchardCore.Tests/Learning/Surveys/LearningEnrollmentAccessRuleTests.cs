using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// A survey owned by a course item can only be started, resumed and completed by a learner enrolled in the course who may
/// open the published item now, for a response that belongs to their own enrollment (or to the course, for an anonymous
/// survey).
/// </summary>
public sealed class LearningEnrollmentAccessRuleTests
{
    private const string UserId = "learner1";

    [Theory]
    [InlineData(CourseItemAccessReason.NotEnrolled)]
    [InlineData(CourseItemAccessReason.NotReleased)]
    [InlineData(CourseItemAccessReason.AccessEnded)]
    [InlineData(CourseItemAccessReason.Suspended)]
    public async Task CheckStart_ALearnerWhoMayNotOpenTheItem_IsRefused(CourseItemAccessReason reason)
    {
        // Arrange
        var rule = Rule(new CourseItemAccess { Reason = reason });

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.Equal(SurveyErrorCode.NotInAudience, decision.Error);
    }

    [Fact]
    public async Task CheckStart_AnEnrolledLearner_ForTheirOwnEnrollment_IsAllowed()
    {
        // Arrange
        var rule = Rule(Enrolled());

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task CheckStart_AnEnrolledLearner_ForSomeoneElsesEnrollment_IsRefused()
    {
        // Arrange
        var rule = Rule(Enrolled());

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("someone-else")));

        // Assert
        Assert.Equal(SurveyErrorCode.NotInAudience, decision.Error);
    }

    [Fact]
    public async Task CheckStart_AnAnonymousCourseSurvey_TakesTheCourseAsItsContext()
    {
        // Arrange
        var rule = Rule(Enrolled());
        var context = Context(LearningSurveyContexts.Course("course1"));
        context.Survey.Access.Anonymity = AnonymityMode.Anonymous;

        // Act
        var decision = await rule.CheckStartAsync(context);

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task CheckStart_SomeoneWhoIsNotSignedIn_MustSignIn()
    {
        // Arrange
        var rule = Rule(Enrolled(), signedIn: false);

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.Equal(SurveyErrorCode.SignInRequired, decision.Error);
    }

    [Fact]
    public async Task CheckStart_ASurveyNotOwnedByACourseItem_OrATestResponse_IsLeftToTheOtherRules()
    {
        // Arrange
        var rule = Rule(new CourseItemAccess { Reason = CourseItemAccessReason.NotEnrolled });
        var standalone = Context(null);
        standalone.Survey.Owner = null;
        var test = Context(LearningSurveyContexts.Enrollment("e1"));
        test.IsTest = true;

        // Act
        var standaloneDecision = await rule.CheckStartAsync(standalone);
        var testDecision = await rule.CheckStartAsync(test);

        // Assert
        Assert.True(standaloneDecision.IsAllowed);
        Assert.True(testDecision.IsAllowed);
    }

    [Fact]
    public async Task CheckStart_AnUnpublishedCourseItem_IsRefused_NotLeftOpen()
    {
        // Arrange
        var rule = Rule(Enrolled(), published: false);

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.Equal(SurveyErrorCode.NotOpen, decision.Error);
    }

    [Fact]
    public async Task CheckStart_ASurveyOwnedByAContentItemThatIsNotACourseItem_IsLeftToTheOtherRules()
    {
        // Arrange
        var rule = Rule(new CourseItemAccess { Reason = CourseItemAccessReason.NotEnrolled }, courseOwned: false);

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Theory]
    [InlineData(CourseItemAccessReason.NotEnrolled)]
    [InlineData(CourseItemAccessReason.Suspended)]
    public async Task CheckResumeAndComplete_ALearnerNoLongerEnrolled_IsRefused(CourseItemAccessReason reason)
    {
        // Arrange
        var rule = Rule(new CourseItemAccess { Reason = reason });
        var context = Context(LearningSurveyContexts.Enrollment("e1"));
        context.Response = new SurveyResponse { ItemId = "r1", Context = context.Context };

        // Act
        var resume = await rule.CheckResumeAsync(context);
        var complete = await rule.CheckCompleteAsync(context);

        // Assert
        Assert.Equal(SurveyErrorCode.NotInAudience, resume.Error);
        Assert.Equal(SurveyErrorCode.NotInAudience, complete.Error);
    }

    [Fact]
    public async Task CheckComplete_AnEnrolledLearner_ForTheirOwnEnrollment_IsAllowed()
    {
        // Arrange
        var rule = Rule(Enrolled());

        // Act
        var decision = await rule.CheckCompleteAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task CheckStart_AnApiCallerIdentifiedOnlyByTheSubClaim_IsRecognized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId)], "Bearer"));
        var rule = Rule(Enrolled(), user: user);

        // Act
        var decision = await rule.CheckStartAsync(Context(LearningSurveyContexts.Enrollment("e1")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    private static CourseItemAccess Enrolled()
        => new()
        {
            Reason = CourseItemAccessReason.Enrolled,
            Enrollment = new Enrollment { ItemId = "e1", CourseId = "course1", UserId = UserId },
            CourseId = "course1",
        };

    private static LearningEnrollmentAccessRule Rule(CourseItemAccess access, bool signedIn = true, bool published = true, bool courseOwned = true, ClaimsPrincipal user = null)
    {
        var item = new ContentItem { ContentItemId = "item1" };
        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys.Setup(service => service.IsOwnedByCourseItemAsync(It.IsAny<Survey>())).ReturnsAsync(courseOwned);
        learningSurveys.Setup(service => service.FindOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync(published ? item : null);

        var courseAccess = new Mock<ICourseAccessService>();
        courseAccess.Setup(service => service.GetItemAccessAsync(It.IsAny<ClaimsPrincipal>(), item)).ReturnsAsync(access);

        user ??= signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };

        return new LearningEnrollmentAccessRule(learningSurveys.Object, courseAccess.Object, accessor);
    }

    private static SurveyAccessContext Context(RecordRef? context)
        => new()
        {
            Survey = new Survey { ItemId = "survey1", Owner = LearningSurveyContexts.Owner("item1") },
            Identity = new SurveyRespondentIdentity { UserId = UserId },
            Context = context,
        };
}
