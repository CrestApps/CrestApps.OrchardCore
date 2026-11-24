using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// What the services read and write: the score and line item rules of Assignment and Grade Services 2.0 (sections 3.2
/// and 3.4, with their example payloads), the line item, result and member JSON, and the paging links of both services.
/// </summary>
public sealed class LtiServicesTests
{
    private static readonly LtiPlatformInfo _platform = new() { Issuer = "https://lms.example.com" };

    [Fact]
    public void TryReadScore_TheSpecificationsExample_IsValid()
    {
        // Arrange: the score example of AGS 2.0 section 3.4.
        var body = JsonNode.Parse("""
            {
              "timestamp": "2017-04-16T18:54:36.736+00:00",
              "scoreGiven": 83,
              "scoreMaximum": 100,
              "comment": "This is exceptional work.",
              "activityProgress": "Completed",
              "gradingProgress": "FullyGraded",
              "userId": "5323497"
            }
            """);

        // Act
        var valid = LtiAgsRules.TryReadScore(body, out var score, out var error);

        // Assert
        Assert.True(valid, error);
        Assert.Equal("5323497", score.UserId);
        Assert.Equal(83, score.ScoreGiven);
        Assert.Equal(100, score.ScoreMaximum);
        Assert.Equal("Completed", score.ActivityProgress);
        Assert.Equal("FullyGraded", score.GradingProgress);
        Assert.Equal(new DateTime(2017, 4, 16, 18, 54, 36, 736, DateTimeKind.Utc), score.TimestampUtc);
        Assert.Equal("This is exceptional work.", score.Comment);
    }

    [Fact]
    public void TryReadScore_WithoutAScore_ForProgressOnly_IsValid()
    {
        // Act
        var valid = LtiAgsRules.TryReadScore(JsonNode.Parse("""{"userId":"u","activityProgress":"Started","gradingProgress":"NotReady","timestamp":"2026-11-16T09:00:00Z"}"""), out var score, out _);

        // Assert
        Assert.True(valid);
        Assert.Null(score.ScoreGiven);
    }

    [Theory]
    [InlineData("""{"activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z"}""")]
    [InlineData("""{"userId":"u","activityProgress":"Finished","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z"}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"Done","timestamp":"2026-11-16T09:00:00Z"}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded"}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00"}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z","scoreGiven":5}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z","scoreGiven":-1,"scoreMaximum":10}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z","scoreGiven":1,"scoreMaximum":0}""")]
    [InlineData("""{"userId":"u","activityProgress":"Completed","gradingProgress":"FullyGraded","timestamp":"2026-11-16T09:00:00Z","scoreGiven":"8","scoreMaximum":10}""")]
    [InlineData("""[]""")]
    public void TryReadScore_AnInvalidScore_IsRefused(string json)
        => Assert.False(LtiAgsRules.TryReadScore(JsonNode.Parse(json), out _, out _));

    [Fact]
    public void TryReadLineItem_TheSpecificationsExample_IsValid()
    {
        // Arrange: the line item example of AGS 2.0 section 3.2.
        var body = JsonNode.Parse("""
            {
              "scoreMaximum": 60,
              "label": "Chapter 5 Test",
              "resourceId": "a-9334df-33",
              "tag": "grade",
              "resourceLinkId": "1g3k4dlk49fk",
              "startDateTime": "2018-03-06T20:05:02Z",
              "endDateTime": "2018-04-06T22:05:03Z"
            }
            """);

        // Act
        var valid = LtiAgsRules.TryReadLineItem(body, out var lineItem, out var error);

        // Assert
        Assert.True(valid, error);
        Assert.Equal(60, lineItem.ScoreMaximum);
        Assert.Equal("Chapter 5 Test", lineItem.Label);
        Assert.Equal("a-9334df-33", lineItem.ResourceId);
        Assert.Equal("grade", lineItem.Tag);
        Assert.Equal("1g3k4dlk49fk", lineItem.ResourceLinkId);
        Assert.Equal(new DateTime(2018, 3, 6, 20, 5, 2, DateTimeKind.Utc), lineItem.StartDateTimeUtc);
        Assert.Equal(new DateTime(2018, 4, 6, 22, 5, 3, DateTimeKind.Utc), lineItem.EndDateTimeUtc);
    }

    [Theory]
    [InlineData("""{"label":"No maximum"}""")]
    [InlineData("""{"scoreMaximum":0,"label":"Zero"}""")]
    [InlineData("""{"scoreMaximum":10}""")]
    [InlineData("""{"scoreMaximum":10,"label":"Bad start","startDateTime":"tomorrow"}""")]
    public void TryReadLineItem_AnInvalidLineItem_IsRefused(string json)
        => Assert.False(LtiAgsRules.TryReadLineItem(JsonNode.Parse(json), out _, out _));

    [Fact]
    public void LineItem_IsWrittenWithItsUrlAsId()
    {
        // Act
        var json = LtiServiceJson.LineItem(_platform, new LtiLineItem
        {
            ItemId = "line-1",
            CourseId = "course-1",
            ScoreMaximum = 60,
            Label = "Chapter 5 Test",
            Tag = "grade",
            ResourceLinkId = "item-1",
            EndDateTimeUtc = new DateTime(2018, 4, 6, 22, 5, 3),
        });

        // Assert
        Assert.Equal("https://lms.example.com/learning/lti/ags/course-1/lineitems/line-1", json["id"]?.GetValue<string>());
        Assert.Equal(60, json["scoreMaximum"]?.GetValue<decimal>());
        Assert.Equal("Chapter 5 Test", json["label"]?.GetValue<string>());
        Assert.Equal("grade", json["tag"]?.GetValue<string>());
        Assert.Equal("item-1", json["resourceLinkId"]?.GetValue<string>());
        Assert.Equal("2018-04-06T22:05:03Z", json["endDateTime"]?.GetValue<string>());
        Assert.Null(json["resourceId"]);
    }

    [Fact]
    public void Result_ScalesTheScoreToTheLineItemsMaximum()
    {
        // Act
        var json = LtiServiceJson.Result(_platform, new LtiLineItem { ItemId = "line-1", CourseId = "course-1", ScoreMaximum = 60 }, new LtiScore
        {
            ItemId = "score-1",
            Subject = "subject-1",
            ScoreGiven = 8,
            ScoreMaximum = 10,
            Comment = "Good",
        });

        // Assert
        Assert.Equal("https://lms.example.com/learning/lti/ags/course-1/lineitems/line-1/results/score-1", json["id"]?.GetValue<string>());
        Assert.Equal("https://lms.example.com/learning/lti/ags/course-1/lineitems/line-1", json["scoreOf"]?.GetValue<string>());
        Assert.Equal("subject-1", json["userId"]?.GetValue<string>());
        Assert.Equal(48, json["resultScore"]?.GetValue<decimal>());
        Assert.Equal(60, json["resultMaximum"]?.GetValue<decimal>());
        Assert.Equal("Good", json["comment"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(LtiPrivacyLevel.Anonymous, false, false)]
    [InlineData(LtiPrivacyLevel.Name, true, false)]
    [InlineData(LtiPrivacyLevel.NameAndEmail, true, true)]
    public void Member_FollowsThePrivacyLevel_AndNeverHasTheUserId(LtiPrivacyLevel privacy, bool hasName, bool hasEmail)
    {
        // Arrange
        var tool = new ExternalTool { ItemId = "tool-1", SubjectSalt = LtiSubjects.NewSalt(), PrivacyLevel = privacy };

        // Act
        var json = LtiServiceJson.Member(new LtiCourseMember
        {
            UserId = "real-user-id",
            Active = true,
            Roles = [LtiConstants.Roles.Learner],
            Name = "Avery Lane",
            GivenName = "Avery",
            FamilyName = "Lane",
            Email = "avery@example.com",
        }, tool);

        // Assert
        Assert.Equal("Active", json["status"]?.GetValue<string>());
        Assert.Equal(LtiSubjects.For(tool, "real-user-id"), json["user_id"]?.GetValue<string>());
        Assert.DoesNotContain("real-user-id", json.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal([LtiConstants.Roles.Learner], json["roles"]!.AsArray().Select(role => role.GetValue<string>()));
        Assert.Equal(hasName, json["name"] is not null);
        Assert.Equal(hasName, json["given_name"] is not null);
        Assert.Equal(hasEmail, json["email"] is not null);
    }

    [Fact]
    public void Read_KeepsTheLimitWithinBounds()
    {
        Assert.Equal((LtiPaging.DefaultLimit, 1), LtiPaging.Read(null, null));
        Assert.Equal((LtiPaging.MaxLimit, 3), LtiPaging.Read("5000", "3"));
        Assert.Equal((LtiPaging.DefaultLimit, 1), LtiPaging.Read("-4", "0"));
        Assert.Equal((10, 2), LtiPaging.Read("10", "2"));
    }

    [Fact]
    public void Link_OnAMiddlePage_NamesTheNextPreviousFirstAndLastPages_KeepingTheFilters()
    {
        // Act
        var link = LtiPaging.Link("https://lms.example.com/learning/lti/nrps/course-1/memberships?role=Learner&limit=10&page=2", 10, 2, 25);

        // Assert
        Assert.Contains("<https://lms.example.com/learning/lti/nrps/course-1/memberships?role=Learner&limit=10&page=3>; rel=\"next\"", link, StringComparison.Ordinal);
        Assert.Contains("<https://lms.example.com/learning/lti/nrps/course-1/memberships?role=Learner&limit=10&page=1>; rel=\"prev\"", link, StringComparison.Ordinal);
        Assert.Contains("page=1>; rel=\"first\"", link, StringComparison.Ordinal);
        Assert.Contains("page=3>; rel=\"last\"", link, StringComparison.Ordinal);
    }

    [Fact]
    public void Link_WhenEverythingFitsOnePage_IsEmpty()
        => Assert.Equal(string.Empty, LtiPaging.Link("https://lms.example.com/x", 100, 1, 12));

    [Fact]
    public void Link_OnTheLastPage_HasNoNext()
        => Assert.DoesNotContain("rel=\"next\"", LtiPaging.Link("https://lms.example.com/x", 10, 3, 25), StringComparison.Ordinal);
}
