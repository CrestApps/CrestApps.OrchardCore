using CrestApps.OrchardCore.Learning.Core.Moodle;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// What the Moodle reader reads for the features the import now maps: the role of a completion by role, an activity's
/// restrictions, the records of a feedback, a choice and an assignment, the course's context and pictures, and the item
/// a file belongs to.
/// </summary>
public sealed class MoodleBackupReaderGapsTests
{
    [Fact]
    public async Task TheReader_ReadsRoles_Restrictions_Records_AndTheCoursePictures()
    {
        // Arrange
        var builder = MoodleBackupSamples.EveryActivity(77, "Every activity");
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(builder.BuildZip()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error + " " + result.ErrorDetail);
        var backup = result.Backup;

        Assert.Equal("editingteacher", Assert.Single(backup.Course.CompletionCriteria, criterion => criterion.Type == MoodleCompletionCriterion.Role).RoleShortName);
        Assert.Equal("25", backup.Course.ContextId);

        var overview = Assert.Single(backup.Files.Values, file => file.Component == "course" && file.FileArea == "overviewfiles");
        Assert.Equal("cover.png", overview.FileName);
        Assert.Equal("25", overview.ContextId);
        Assert.NotNull(backup.GetContentPath(overview));

        var feedback = Assert.Single(backup.Activities, activity => activity.ModuleName == "feedback");
        Assert.Equal(3, feedback.RecordsOf("item").Count);
        Assert.Equal("multichoice", feedback.RecordsOf("item")[0]["typ"]);

        var choice = Assert.Single(backup.Activities, activity => activity.ModuleName == "choice");
        Assert.Equal(["Monday", "Friday"], choice.RecordsOf("option").Select(option => option["text"]));

        var assign = Assert.Single(backup.Activities, activity => activity.ModuleName == "assign");
        Assert.Contains(assign.RecordsOf("plugin_config"), config => config["plugin"] == "file" && config["name"] == "enabled" && config["value"] == "1");

        var restricted = Assert.Single(backup.Activities, activity => activity.Name == "For Ana only");
        Assert.Contains("\"sf\":\"email\"", restricted.Availability, StringComparison.Ordinal);
        Assert.Empty(feedback.Availability ?? string.Empty);

        var label = Assert.Single(backup.Activities, activity => activity.ModuleName == "label");
        var picture = Assert.Single(label.Files);
        Assert.Equal(("mod_label", "intro", "exit.png"), (picture.Component, picture.FileArea, picture.FileName));
        Assert.Equal("0", picture.ItemId);
    }
}
