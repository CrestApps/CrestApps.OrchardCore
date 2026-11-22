using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyAccessRulesTests
{
    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(8, 59, false)]
    [InlineData(17, 0, false)]
    public void WeeklySchedule_Between_AllowsOnlyTheRange(int hour, int minute, bool expected)
    {
        // Arrange
        var days = new Dictionary<DayOfWeek, DaySchedule>
        {
            [DayOfWeek.Wednesday] = new DaySchedule { Mode = DayScheduleMode.Between, From = new TimeOnly(9, 0), To = new TimeOnly(17, 0) },
            [DayOfWeek.Sunday] = new DaySchedule { Mode = DayScheduleMode.PreventAll },
        };

        // Act
        var allowed = WeeklyScheduleRule.IsAllowed(days, new DateTime(2026, 11, 18, hour, minute, 0));

        // Assert
        Assert.Equal(expected, allowed);
        Assert.False(WeeklyScheduleRule.IsAllowed(days, new DateTime(2026, 11, 22, 12, 0, 0)));
        Assert.True(WeeklyScheduleRule.IsAllowed(days, new DateTime(2026, 11, 19, 3, 0, 0)));
    }

    [Fact]
    public async Task WeeklySchedule_UsesTheSiteTimeZone_AcrossDaylightSaving()
    {
        // Arrange: 09:30 New York is 14:30 UTC in winter and 13:30 UTC in summer.
        var now = new DateTime(2026, 11, 2, 14, 30, 0, DateTimeKind.Utc);
        var site = new Mock<ISite>();
        site.SetupGet(value => value.TimeZoneId).Returns(TimeZoneId());
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        var rule = new WeeklyScheduleRule(siteService.Object, Mock.Of<IClock>());
        var survey = new Survey
        {
            Access = new SurveyAccessSettings
            {
                Schedule = new WeeklySchedule
                {
                    Days = new Dictionary<DayOfWeek, DaySchedule>
                    {
                        [DayOfWeek.Monday] = new DaySchedule { Mode = DayScheduleMode.Between, From = new TimeOnly(9, 0), To = new TimeOnly(10, 0) },
                    },
                },
            },
        };

        // Act
        var winter = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = now });
        var summerTooLate = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc) });
        var summer = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = new DateTime(2026, 6, 1, 13, 30, 0, DateTimeKind.Utc) });

        // Assert
        Assert.True(winter.IsAllowed);
        Assert.Equal(SurveyErrorCode.NotAvailableNow, summerTooLate.Error);
        Assert.True(summer.IsAllowed);
    }

    [Fact]
    public async Task AvailabilityWindow_RefusesBeforeOpenAndAfterClose()
    {
        // Arrange
        var rule = new AvailabilityWindowRule();
        var survey = new Survey { Access = new SurveyAccessSettings { OpensUtc = RuntimeTestHarness.Now, ClosesUtc = RuntimeTestHarness.Now.AddDays(1) } };

        // Act
        var early = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = RuntimeTestHarness.Now.AddMinutes(-1) });
        var open = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = RuntimeTestHarness.Now });
        var late = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = RuntimeTestHarness.Now.AddDays(1) });
        var extended = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, UtcNow = RuntimeTestHarness.Now.AddDays(1), Override = new SurveyOverride { ClosesUtc = RuntimeTestHarness.Now.AddDays(2) } });

        // Assert
        Assert.Equal(SurveyErrorCode.NotOpen, early.Error);
        Assert.True(open.IsAllowed);
        Assert.Equal(SurveyErrorCode.Closed, late.Error);
        Assert.True(extended.IsAllowed);
    }

    [Fact]
    public async Task Password_RequiresAndVerifiesTheCollectorPassword()
    {
        // Arrange
        var rule = new PasswordRule();
        var collector = new SurveyCollector();
        collector.PasswordHash = PasswordRule.HashPassword(collector, "open sesame");
        var survey = new Survey();

        // Act
        var missing = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, Collector = collector });
        var wrong = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, Collector = collector, Password = "nope" });
        var right = await rule.CheckStartAsync(new SurveyAccessContext { Survey = survey, Collector = collector, Password = "open sesame" });

        // Assert
        Assert.Equal(SurveyErrorCode.PasswordRequired, missing.Error);
        Assert.Equal(SurveyErrorCode.PasswordInvalid, wrong.Error);
        Assert.True(right.IsAllowed);
    }

    [Fact]
    public void RespondentKeys_AnonymousSurvey_NeverContainsTheUserId()
    {
        // Arrange
        var survey = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };

        // Act
        var key = SurveyRespondentKeys.For(survey, "secret", new SurveyRespondentIdentity { UserId = "user-1" }, null);
        var again = SurveyRespondentKeys.For(survey, "secret", new SurveyRespondentIdentity { UserId = "user-1" }, null);

        // Assert
        Assert.DoesNotContain("user-1", key, StringComparison.Ordinal);
        Assert.Equal(key, again);
        Assert.True(key.Length <= 64);
    }

    [Theory]
    [InlineData("/thanks", true)]
    [InlineData("//evil.example/x", false)]
    [InlineData("https://site.example/x", true)]
    [InlineData("https://partner.example/x", true)]
    [InlineData("https://evil.example/x", false)]
    [InlineData("javascript:alert(1)", false)]
    public void EndingRedirect_IsLocalOrAllowed(string url, bool expected)
        => Assert.Equal(expected, SurveyEndingService.IsAllowedRedirect(url, "site.example", ["partner.example"]));

    [Fact]
    public void ResumeTokens_FindTheirResponse_AndMatchOnlyTheirHash()
    {
        // Arrange
        var token = SurveyResumeTokens.Issue("response1");

        // Act
        var hash = SurveyResumeTokens.Hash(token);

        // Assert
        Assert.Equal("response1", SurveyResumeTokens.ResponseIdOf(token));
        Assert.True(SurveyResumeTokens.Matches(token, hash));
        Assert.False(SurveyResumeTokens.Matches(token + "x", hash));
    }

    private static string TimeZoneId()
        => OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York";
}
