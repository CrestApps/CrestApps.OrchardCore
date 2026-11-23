using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.Settings;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The lower findings of the runtime production review: close rules on completion, the Uniqueness setting, page
/// shuffle with branches, the site's calendar in formulas, carried-forward choices, piped plain texts and the rate-limit
/// partition of a started response.
/// </summary>
public sealed class SurveyRuntimeProductionReviewTests
{
    private static readonly DateTime _now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StatusAndWindow_RefuseARespondentsCompletionAfterTheClose_ButNotASubmissionForThem()
    {
        // Arrange
        var survey = new Survey { Status = SurveyStatus.Open, Access = new SurveyAccessSettings { ClosesUtc = _now.AddHours(-1) } };
        var closedSurvey = new Survey { Status = SurveyStatus.Closed, Access = new SurveyAccessSettings() };
        var version = new SurveyVersion { Definition = new SurveyDefinition() };

        SurveyAccessContext Context(Survey item, bool autoSubmitted) => new() { Survey = item, Version = version, UtcNow = _now, AutoSubmitted = autoSubmitted };

        // Act
        var windowRefuses = await new AvailabilityWindowRule().CheckCompleteAsync(Context(survey, autoSubmitted: false));
        var windowLetsThrough = await new AvailabilityWindowRule().CheckCompleteAsync(Context(survey, autoSubmitted: true));
        var statusRefuses = await new SurveyStatusRule().CheckCompleteAsync(Context(closedSurvey, autoSubmitted: false));
        var statusLetsThrough = await new SurveyStatusRule().CheckCompleteAsync(Context(closedSurvey, autoSubmitted: true));
        var accommodated = await new AvailabilityWindowRule().CheckCompleteAsync(new SurveyAccessContext
        {
            Survey = survey,
            Version = version,
            UtcNow = _now,
            Override = new SurveyOverride { ClosesUtc = _now.AddHours(1) },
        });

        // Assert
        Assert.Equal(SurveyErrorCode.Closed, windowRefuses.Error);
        Assert.True(windowLetsThrough.IsAllowed);
        Assert.Equal(SurveyErrorCode.Closed, statusRefuses.Error);
        Assert.True(statusLetsThrough.IsAllowed);
        Assert.True(accommodated.IsAllowed);
    }

    [Theory]
    [InlineData(UniquenessMode.SignedInUser, "user", "invite", "browser", "u:user")]
    [InlineData(UniquenessMode.SignedInUser, null, "invite", "browser", "i:invite")]
    [InlineData(UniquenessMode.SignedInUser, null, null, "browser", null)]
    [InlineData(UniquenessMode.Invitation, "user", "invite", null, "i:invite")]
    [InlineData(UniquenessMode.Cookie, null, null, "browser", "b:browser")]
    [InlineData(UniquenessMode.None, null, "invite", "browser", null)]
    [InlineData(UniquenessMode.None, "user", "invite", "browser", "u:user")]
    public void RespondentKey_FollowsTheUniquenessSetting(UniquenessMode mode, string userId, string invitationId, string browserKey, string expected)
    {
        // Arrange
        var survey = new Survey { Access = new SurveyAccessSettings { Limits = new RespondentLimits { Uniqueness = mode } } };
        var invitation = invitationId is null ? null : new SurveyInvitation { ItemId = invitationId };

        // Act
        var key = SurveyRespondentKeys.For(survey, "secret", new SurveyRespondentIdentity { UserId = userId, BrowserKey = browserKey }, invitation);

        // Assert
        Assert.Equal(expected, key);
    }

    [Fact]
    public async Task PlanBuilder_KeepsTheDesignedPageOrder_WhenAPageBranchesToAnother()
    {
        // Arrange: page shuffle with a branch from p2 to p4.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")),
            RuntimeSurveys.Page("p3", RuntimeSurveys.Text("q3")),
            RuntimeSurveys.Page("p4", RuntimeSurveys.Text("q4")),
            RuntimeSurveys.Page("p5", RuntimeSurveys.Text("q5")));
        definition.Presentation.PageOrder = QuestionOrder.Shuffle;
        definition.Pages[1].Branches.Add(new SurveyBranch { TargetKind = BranchTargetKind.Page, TargetId = "p4" });

        // Act
        var plans = await harness.RunAsync(services =>
        {
            var builder = services.GetRequiredService<SurveyPlanBuilder>();

            return Task.FromResult(Enumerable.Range(1, 20).Select(seed => builder.Build("s", definition, seed)).ToList());
        });

        // Assert
        Assert.True(SurveyPlanBuilder.KeepsPageOrder(definition));
        Assert.All(plans, plan => Assert.Equal(["p1", "p2", "p3", "p4", "p5"], plan.PageOrder));
    }

    [Fact]
    public async Task Age_CountsOnTheSitesCalendar_NotUtc()
    {
        // Arrange: 17:00 in Los Angeles on 22 November is already the 23rd in UTC; the birthday is the 23rd.
        var losAngeles = OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles";
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            var site = new Mock<ISite>();
            site.SetupGet(value => value.TimeZoneId).Returns(losAngeles);
            var siteService = new Mock<ISiteService>();
            siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
            services.RemoveAll<ISiteService>();
            services.AddSingleton(siteService.Object);
        });
        harness.Clock.UtcNow = new DateTime(2026, 11, 23, 1, 0, 0, DateTimeKind.Utc);
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("born")));
        var response = new SurveyResponse();
        response.Answers["born"] = new SurveyAnswer { Values = ["2008-11-23"] };

        // Act
        var age = await harness.RunAsync(async services =>
        {
            var formulas = services.GetRequiredService<SurveyFormulaService>();
            await formulas.PrepareAsync();

            return formulas.Evaluate(definition, response, "AGE([born])");
        });

        // Assert
        Assert.Equal(17, Convert.ToInt32(age, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ChoiceHandler_AnEmptyAllowedList_AllowsNoChoice()
    {
        // Arrange: a carried-forward question with nothing carried forward.
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("b", "B"));
        var read = Read(question, Form(("q.q1", ["a"])));
        read.AllowedChoiceIds = [];
        var validate = Validate(question, new SurveyAnswer { Values = ["a"] });
        validate.AllowedChoiceIds = [];
        var handler = ControlsTestServices.Build().CreateScope().ServiceProvider.GetServices<ISurveyControlHandler>().OfType<SingleChoiceControlHandler>().Single();

        // Act
        await handler.ReadAnswerAsync(read);
        await handler.ValidateAnswerAsync(validate);

        // Assert
        Assert.Null(read.Answer);
        Assert.NotEmpty(validate.Errors);
    }

    [Fact]
    public async Task CarriedForward_NothingCarried_HidesTheRequiredQuestion_AndThePageGoesOn()
    {
        // Arrange: q2 shows only what q1 selected, and is required; q1 is optional.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var carried = RuntimeSurveys.Choice("q2", "a", "b");
        carried.ChoicesFrom = "q1";
        carried.ChoicesFromMode = ChoicesFromMode.Selected;
        carried.Required = true;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a", "b")),
            RuntimeSurveys.Page("p2", carried)));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var start = await harness.StartAsync(survey, "u1");

        // Act
        var second = await harness.PostAsync(start.Response, identity, SurveyPageDirection.Next);
        var finished = await harness.PostAsync(second.Response, identity, SurveyPageDirection.Finish);

        // Assert
        Assert.Contains("q2", second.Page.HiddenQuestionIds);
        Assert.True(finished.Ended);
    }

    [Fact]
    public async Task PipedChoiceText_IsPlainText_WhileThePipedTitleIsEncoded()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var piped = RuntimeSurveys.Choice("q2", "a");
        piped.Title = "Hello {q1}";
        piped.Choices[0].Text = "Yes, {q1}";
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")),
            RuntimeSurveys.Page("p2", piped)));
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var start = await harness.StartAsync(survey, "u1");

        // Act
        var second = await harness.PostAsync(start.Response, identity, SurveyPageDirection.Next, ("q1", "O'Brien & Co"));
        var question = Assert.Single(second.Page.Questions);

        // Assert
        Assert.Equal("Yes, O'Brien & Co", Assert.Single(question.Choices).Text);
        Assert.Equal("Hello O&#39;Brien &amp; Co", question.Title);
    }

    [Fact]
    public async Task AnonymousSurvey_ASignedInCallerWithTheResumeToken_ResumesInsteadOfStartingAnother()
    {
        // Arrange: one attempt, counting started responses, as an API client would hit it.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(
            RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
            item =>
            {
                item.Access.Anonymity = AnonymityMode.Anonymous;
                item.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Started };
            });

        // Act
        var first = await harness.StartAsync(survey, "u1");
        var again = await harness.StartAsync(survey, "u1", request => request.Identity.ResumeToken = first.ResumeToken);
        var finished = await harness.PostAsync(again.Response, new SurveyRespondentIdentity { UserId = "u1", ResumeToken = first.ResumeToken }, SurveyPageDirection.Finish, ("q1", "a"));

        // Assert
        Assert.False(string.IsNullOrEmpty(first.ResumeToken));
        Assert.True(again.Resumed);
        Assert.Equal(first.Response.ItemId, again.Response.ItemId);
        Assert.True(finished.Ended);
    }

    [Fact]
    public void ApiIdentity_CarriesTheResumeTokenHeader()
    {
        // Arrange
        var context = new DefaultHttpContext { User = RuntimeTestHarness.User("u1") };
        context.Request.Headers[CrestApps.OrchardCore.Surveys.Api.SurveysApiEndpoints.ResumeTokenHeader] = " r1.secret ";
        var withoutToken = new DefaultHttpContext { User = RuntimeTestHarness.User("u1") };

        // Act
        var identity = CrestApps.OrchardCore.Surveys.Api.SurveysApiEndpoints.Identity(context);
        var plain = CrestApps.OrchardCore.Surveys.Api.SurveysApiEndpoints.Identity(withoutToken);

        // Assert
        Assert.Equal("u1", identity.UserId);
        Assert.Equal("r1.secret", identity.ResumeToken);
        Assert.Null(plain.ResumeToken);
    }

    [Fact]
    public void ResponseRoutes_AreCountedPerResponse()
    {
        // Arrange
        var first = new DefaultHttpContext();
        first.Request.RouteValues["responseId"] = "r1";
        var second = new DefaultHttpContext();
        second.Request.RouteValues["responseId"] = "r2";

        // Act
        var firstKey = RespondRateLimitsStartup.ResponsePartitionKey(first);
        var secondKey = RespondRateLimitsStartup.ResponsePartitionKey(second);

        // Assert
        Assert.StartsWith("response:r1|", firstKey, StringComparison.Ordinal);
        Assert.NotEqual(firstKey, secondKey);
        Assert.True(RespondRateLimitsStartup.ResponseRequestsPerMinute >= RespondRateLimitsStartup.RequestsPerMinute);
    }

    [Fact]
    public void ResponseRoutes_AStrangerWithTheVictimsResponseId_CountsInABudgetOfTheirOwn()
    {
        // Arrange: the respondent carries the response's resume cookie; a stranger (another address) only knows the id.
        var respondent = new DefaultHttpContext();
        respondent.Request.RouteValues["responseId"] = "r1";
        respondent.Request.Headers.Cookie = "sr_s1=r1.secret~g1";
        respondent.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");

        var sameRespondentOtherAddress = new DefaultHttpContext();
        sameRespondentOtherAddress.Request.RouteValues["responseId"] = "r1";
        sameRespondentOtherAddress.Request.Headers.Cookie = "sr_s1=r1.secret~g1";
        sameRespondentOtherAddress.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.9");

        var stranger = new DefaultHttpContext();
        stranger.Request.RouteValues["responseId"] = "r1";
        stranger.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.2");

        var apiClient = new DefaultHttpContext();
        apiClient.Request.RouteValues["responseId"] = "r1";
        apiClient.Request.Headers[CrestApps.OrchardCore.Surveys.Api.SurveysApiEndpoints.ResumeTokenHeader] = "r1.secret";

        // Act
        var respondentKey = RespondRateLimitsStartup.ResponsePartitionKey(respondent);
        var strangerKey = RespondRateLimitsStartup.ResponsePartitionKey(stranger);

        // Assert: the token, not the address, names the respondent; the stranger has another key; the secret isn't in it.
        Assert.Equal(respondentKey, RespondRateLimitsStartup.ResponsePartitionKey(sameRespondentOtherAddress));
        Assert.Equal(respondentKey, RespondRateLimitsStartup.ResponsePartitionKey(apiClient));
        Assert.NotEqual(respondentKey, strangerKey);
        Assert.DoesNotContain("secret", respondentKey, StringComparison.Ordinal);
    }
}
