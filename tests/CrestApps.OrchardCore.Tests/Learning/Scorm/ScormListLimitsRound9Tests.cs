using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Review round 9, M4: the lists of the runtime data take a bounded number of entries (the SCORM 2004 smallest permitted
/// maximums), so a learner cannot grow the runtime state without limit; a write past a list's limit gets the
/// standard's error code.
/// </summary>
public sealed class ScormListLimitsRound9Tests
{
    [Fact]
    public void Round9M4_Scorm12_StopsAddingInteractionsObjectivesAndCorrectResponses_AtTheirLimits()
    {
        var cmi = new Scorm12Cmi();

        for (var index = 0; index < 250; index++)
        {
            Assert.Equal(Scorm12Errors.NoError, Scorm12DataModel.Set(cmi, $"cmi.interactions.{index}.id", "i" + index));
        }

        for (var index = 0; index < 100; index++)
        {
            Assert.Equal(Scorm12Errors.NoError, Scorm12DataModel.Set(cmi, $"cmi.objectives.{index}.id", "o" + index));
        }

        for (var index = 0; index < 10; index++)
        {
            Assert.Equal(Scorm12Errors.NoError, Scorm12DataModel.Set(cmi, $"cmi.interactions.0.correct_responses.{index}.pattern", "a"));
            Assert.Equal(Scorm12Errors.NoError, Scorm12DataModel.Set(cmi, $"cmi.interactions.0.objectives.{index}.id", "o" + index));
        }

        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.250.id", "toomany"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.objectives.100.id", "toomany"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.0.correct_responses.10.pattern", "a"));
        Assert.Equal(Scorm12Errors.InvalidArgument, Scorm12DataModel.Set(cmi, "cmi.interactions.0.objectives.10.id", "o"));
        Assert.Equal(250, cmi.Interactions.Count);
        Assert.Equal(100, cmi.Objectives.Count);
    }

    [Fact]
    public async Task Round9M4_AChangeThatMakesTheStateTooLarge_IsRefusedWhole_AndNothingIsSaved()
    {
        // Arrange: 250 interactions with the longest id and learner response SCORM 2004 allows, about 2 MB.
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", PackageTestHarness.Package2004);
        var changes = new List<(string Element, string Value)>();

        for (var index = 0; index < 250; index++)
        {
            changes.Add(($"cmi.interactions.{index}.id", "i" + index + new string('x', 3990)));
            changes.Add(($"cmi.interactions.{index}.type", "long-fill-in"));
            changes.Add(($"cmi.interactions.{index}.learner_response", new string('y', 3990)));
        }

        // Act
        var result = await h.ApplyAsync("ana", launch, commit: true, changes: [.. changes]);
        var state = await h.StateAsync(enrollment.ItemId, PackageTestHarness.Package2004);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("StateTooLarge", result.Error);
        Assert.Empty(state.Cmi2004.Interactions);
    }

    [Fact]
    public void Round9M4_Scorm2004_StopsAddingEntries_AtTheirLimits_WithAGeneralSetFailure()
    {
        var cmi = new Scorm2004Cmi();

        for (var index = 0; index < 250; index++)
        {
            Assert.Equal(Scorm2004Errors.NoError, Scorm2004DataModel.Set(cmi, $"cmi.interactions.{index}.id", "i" + index));
            Assert.Equal(Scorm2004Errors.NoError, Scorm2004DataModel.Set(cmi, $"cmi.comments_from_learner.{index}.comment", "c" + index));
        }

        for (var index = 0; index < 100; index++)
        {
            Assert.Equal(Scorm2004Errors.NoError, Scorm2004DataModel.Set(cmi, $"cmi.objectives.{index}.id", "o" + index));
        }

        Assert.Equal(Scorm2004Errors.NoError, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.type", "fill-in"));

        for (var index = 0; index < 10; index++)
        {
            Assert.Equal(Scorm2004Errors.NoError, Scorm2004DataModel.Set(cmi, $"cmi.interactions.0.objectives.{index}.id", "o" + index));
        }

        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.250.id", "toomany"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.comments_from_learner.250.comment", "toomany"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.objectives.100.id", "toomany"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, Scorm2004DataModel.Set(cmi, "cmi.interactions.0.objectives.10.id", "toomany"));
        Assert.Equal(250, cmi.Interactions.Count);
        Assert.Equal(250, cmi.CommentsFromLearner.Count);
        Assert.Equal(100, cmi.Objectives.Count);
    }
}
