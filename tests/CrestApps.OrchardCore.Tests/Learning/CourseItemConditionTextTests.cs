using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Each item says what completes it, as Moodle's completion conditions do: the player and the course page show it beside
/// the item, with its done state.
/// </summary>
public sealed class CourseItemConditionTextTests
{
    [Theory]
    [InlineData(CompletionModes.MarkedDone, null, "Mark it as done")]
    [InlineData(CompletionModes.Viewed, null, "View it")]
    [InlineData(CompletionModes.Acknowledged, null, "Confirm the statement")]
    [InlineData(CompletionModes.Passed, "70", "Pass with 70%")]
    [InlineData(CompletionModes.Passed, null, "Pass it")]
    [InlineData(CompletionModes.MinimumScore, "90", "Receive a score of 90% or more")]
    [InlineData(CompletionModes.Submitted, null, "Submit it")]
    [InlineData(CompletionModes.PackageReported, null, "Complete the activity")]
    public void Get_SaysWhatCompletesTheItem(string mode, string passMark, string expected)
    {
        // Arrange
        var text = new CourseItemConditionText(new EchoLocalizer());
        var entry = Entry(part =>
        {
            part.CompletionMode = mode;
            part.PassMark = passMark is null ? null : decimal.Parse(passMark, System.Globalization.CultureInfo.InvariantCulture);
        });

        // Act
        var condition = text.Get(entry);

        // Assert
        Assert.Equal(expected, condition.Value);
    }

    [Fact]
    public void Get_ForAVideo_SaysTheShareToWatch_AndNothingForTextOrAnUnknownMode()
    {
        // Arrange
        var text = new CourseItemConditionText(new EchoLocalizer());
        var video = Entry(part => part.CompletionMode = CompletionModes.Watched);
        video.Item.Alter<VideoLessonPart>(part => part.RequiredPercent = 80);
        var inline = Entry(part => part.CompletionMode = CompletionModes.Viewed);
        inline.Inline = true;

        // Act & Assert
        Assert.Equal("Watch 80% of the video", text.Get(video).Value);
        Assert.Null(text.Get(inline));
        Assert.Null(text.Get(Entry(part => part.CompletionMode = "SomethingElse")));
    }

    private static CourseOutlineEntry Entry(Action<CourseItemPart> configure)
    {
        var item = new ContentItem { ContentItemId = "item", ContentType = "PageLesson" };

        item.Alter<CourseItemPart>(configure);

        return new CourseOutlineEntry { Item = item, Part = item.Get<CourseItemPart>(nameof(CourseItemPart)), Required = true };
    }

    private sealed class EchoLocalizer : IStringLocalizer<CourseItemConditionText>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
