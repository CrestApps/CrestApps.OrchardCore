using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The one small-group rule of anonymous surveys every output shares, and the label staff see for a response.
/// </summary>
public sealed class SurveyGroupGateTests
{
    [Fact]
    public void Decide_BelowTheMinimumOfCompletedResponses_HidesAll()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [], Counts(completed: 4, inProgress: 20));

        // Assert
        Assert.True(decision.HidesAll);
        Assert.Equal(5, decision.MinimumGroupSize);
    }

    [Fact]
    public void Decide_EveryStatus_LeavesOutEachStatusTooFewShare()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [], Counts(completed: 5, inProgress: 5, abandoned: 1));

        // Assert
        Assert.False(decision.HidesAll);
        Assert.Equal([SurveyResponseStatus.Abandoned], decision.HiddenStatuses);
        Assert.True(decision.Shows(SurveyResponseStatus.InProgress, isTest: false));
        Assert.False(decision.Shows(SurveyResponseStatus.Abandoned, isTest: false));
        Assert.True(decision.Shows(SurveyResponseStatus.Abandoned, isTest: true));
    }

    [Fact]
    public void Decide_FilteredToAStatusTooFewShare_HidesAll()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [SurveyResponseStatus.Abandoned], Counts(completed: 9, abandoned: 1));

        // Assert
        Assert.True(decision.HidesAll);
    }

    [Fact]
    public void Decide_FinishedWithFewScreenedOut_KeepsTheCompletedRows()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [SurveyResponseStatus.Completed, SurveyResponseStatus.ScreenedOut], Counts(completed: 6, screenedOut: 2));

        // Assert
        Assert.False(decision.HidesAll);
        Assert.Equal([SurveyResponseStatus.ScreenedOut], decision.HiddenStatuses);
    }

    [Fact]
    public void Decide_WithoutAMinimum_IsOpen()
    {
        // Act
        var decision = SurveyGroupGate.Decide(0, [], Counts(completed: 1, abandoned: 1));

        // Assert
        Assert.Same(SurveyGroupDecision.Open, decision);
    }

    [Fact]
    public async Task DecideAsync_OnAnIdentifiedSurvey_CountsNothing()
    {
        // Arrange
        var survey = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified } };

        // Act
        var decision = await SurveyGroupGate.DecideAsync(survey, null, [], _ => throw new InvalidOperationException("Counted."));

        // Assert
        Assert.False(decision.HidesAll);
    }

    [Fact]
    public void ResponseLabel_OnAnAnonymousSurvey_IsAKeyedHashOfTheId_NotTheSiteWideNumberNorTheIdPrefix()
    {
        // Arrange
        var anonymous = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        var identified = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified } };
        var response = new SurveyResponse { ItemId = "4k7x2m9qabcdefghjkmnpqrstv", Number = "R0004212" };

        // Act
        var label = SurveyAnonymity.ResponseLabel(anonymous, response, "secret-1");

        // Assert: R6-L14, the id is in the response's links (and so in access logs); the label can't be read from it.
        Assert.Equal(SurveyAnonymity.AnonymousLabelLength, label.Length);
        Assert.Matches("^[0-9a-hjkmnp-tv-z]+$", label);
        Assert.DoesNotContain(label, response.ItemId, StringComparison.Ordinal);
        Assert.Equal(label, SurveyAnonymity.ResponseLabel(null, response, "secret-1"));
        Assert.NotEqual(label, SurveyAnonymity.ResponseLabel(anonymous, response, "secret-2"));
        Assert.NotEqual(label, SurveyAnonymity.ResponseLabel(anonymous, new SurveyResponse { ItemId = "4k7x2m9qabcdefghjkmnpqrstw" }, "secret-1"));
        Assert.Equal(string.Empty, SurveyAnonymity.ResponseLabel(anonymous, response, null));
        Assert.Equal("R0004212", SurveyAnonymity.ResponseLabel(identified, response, "secret-1"));
    }

    [Fact]
    public void WholeDays_WidensATimeOfDayToTheDaysInTheSiteTimeZone()
    {
        // Arrange: a site at UTC-5 (Bogotá keeps no daylight saving); a filter from 09:30 to 10:07 local.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var from = new DateTime(2026, 1, 10, 14, 30, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 10, 15, 7, 0, DateTimeKind.Utc);

        // Act
        var (wideFrom, wideTo) = SurveyAnonymity.WholeDays(from, to, zone);
        var (_, midnight) = SurveyAnonymity.WholeDays(null, new DateTime(2026, 1, 11, 5, 0, 0, DateTimeKind.Utc), zone);

        // Assert: R6-M2, the local day of 10 January, 05:00 to 05:00 UTC; an end on a local midnight stays.
        Assert.Equal(new DateTime(2026, 1, 10, 5, 0, 0, DateTimeKind.Utc), wideFrom);
        Assert.Equal(new DateTime(2026, 1, 11, 5, 0, 0, DateTimeKind.Utc), wideTo);
        Assert.Equal(new DateTime(2026, 1, 11, 5, 0, 0, DateTimeKind.Utc), midnight);
        Assert.Equal((null, null), SurveyAnonymity.WholeDays(null, null, zone));
    }

    [Fact]
    public void AnonymousAttributes_ALinkSentToOnePerson_HidesTheirResponse()
    {
        // Arrange: R6-M3, a link "for Bob" with one completed response among 6 on the main link; the minimum is 5.
        var attributes = SurveyAnonymousAttributes.Create(
            5,
            new Dictionary<string, long> { ["main"] = 6, ["bob"] = 1 },
            new Dictionary<string, long> { ["1"] = 7 },
            new Dictionary<string, long> { [string.Empty] = 7 });

        // Act
        var bob = attributes.Of(new SurveyResponse { ItemId = "b", CollectorId = "bob", VersionNumber = 1, Status = SurveyResponseStatus.Completed });
        var other = attributes.Of(new SurveyResponse { ItemId = "m", CollectorId = "main", VersionNumber = 1, Status = SurveyResponseStatus.Completed });

        // Assert: "Other" would still be Bob alone, so his response doesn't show at all; the others show every value.
        Assert.True(bob.Hidden);
        Assert.Equal(new SurveyRowAttributes(false, true, true, true), other);
    }

    [Fact]
    public void AnonymousAttributes_SmallGroupsLargeEnoughTogether_ShowAsOther_SoSubtractionFindsNoOne()
    {
        // Arrange: two small links of 3 (6 together) next to a large one, and a version only one person answered.
        var attributes = SurveyAnonymousAttributes.Create(
            5,
            new Dictionary<string, long> { ["main"] = 6, ["a"] = 3, ["b"] = 3 },
            new Dictionary<string, long> { ["1"] = 11, ["2"] = 1 },
            new Dictionary<string, long> { [string.Empty] = 12 });

        // Act
        var small = attributes.Of(new SurveyResponse { ItemId = "a1", CollectorId = "a", VersionNumber = 1 });
        var lone = attributes.Of(new SurveyResponse { ItemId = "v2", CollectorId = "main", VersionNumber = 2 });
        var test = attributes.Of(new SurveyResponse { ItemId = "t", CollectorId = "bob", VersionNumber = 2, IsTest = true });

        // Assert: the small links show as Other (6 hide in it); the lone version can't hide, so that response doesn't
        // show; a test response is never hidden on its own but still masks.
        Assert.Equal(new SurveyRowAttributes(false, false, true, true), small);
        Assert.True(lone.Hidden);
        Assert.False(test.Hidden);
        Assert.False(test.ShowsCollector);
        Assert.False(test.ShowsVersion);
        Assert.Equal(SurveyRowAttributes.All, SurveyAnonymousAttributes.Open.Of(new SurveyResponse { CollectorId = "bob" }));
    }

    private static Dictionary<SurveyResponseStatus, long> Counts(long completed = 0, long inProgress = 0, long abandoned = 0, long screenedOut = 0)
        => new()
        {
            [SurveyResponseStatus.Completed] = completed,
            [SurveyResponseStatus.InProgress] = inProgress,
            [SurveyResponseStatus.Abandoned] = abandoned,
            [SurveyResponseStatus.ScreenedOut] = screenedOut,
        };
}
