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
/// A survey owned by a course item can only be started by a learner enrolled in the course who may open the item now,
/// for a response that belongs to their own enrollment (or to the course, for an anonymous survey).
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

    private static CourseItemAccess Enrolled()
        => new()
        {
            Reason = CourseItemAccessReason.Enrolled,
            Enrollment = new Enrollment { ItemId = "e1", CourseId = "course1", UserId = UserId },
            CourseId = "course1",
        };

    private static LearningEnrollmentAccessRule Rule(CourseItemAccess access, bool signedIn = true)
    {
        var item = new ContentItem { ContentItemId = "item1" };
        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys.Setup(service => service.FindOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync(item);

        var courseAccess = new Mock<ICourseAccessService>();
        courseAccess.Setup(service => service.GetItemAccessAsync(It.IsAny<ClaimsPrincipal>(), item)).ReturnsAsync(access);

        var user = signedIn
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
