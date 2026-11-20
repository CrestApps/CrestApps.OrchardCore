using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Remaining work section 3, item 11: lesson files and videos over Orchard Core Media's 30 MB default, OpenDocument
/// and other office files, and the original file name shown to learners instead of the stored name.
/// </summary>
public sealed class LearningMediaTests
{
    [Fact]
    public void LearningLimit_RaisesTheMediaUploadLimit_AboveThirtyMegabytes()
    {
        // Arrange
        var media = new MediaOptions { MaxFileSize = 30_000_000, AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };

        // Act
        Configure(media, new LearningMediaOptions());

        // Assert
        Assert.Equal(LearningMediaOptions.DefaultMaxFileSize, media.MaxFileSize);
        Assert.True(media.MaxFileSize > 1_000_000_000);
    }

    [Fact]
    public void LargerMediaLimit_IsKept()
    {
        // Arrange
        var media = new MediaOptions { MaxFileSize = 5_000_000_000, AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };

        // Act
        Configure(media, new LearningMediaOptions { MaxFileSize = 200_000_000 });

        // Assert
        Assert.Equal(5_000_000_000, media.MaxFileSize);
    }

    [Fact]
    public void ZeroLimit_LeavesTheMediaLimitAlone()
    {
        // Arrange
        var media = new MediaOptions { MaxFileSize = 30_000_000, AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };

        // Act
        Configure(media, new LearningMediaOptions { MaxFileSize = 0 });

        // Assert
        Assert.Equal(30_000_000, media.MaxFileSize);
    }

    [Theory]
    [InlineData(".ods")]
    [InlineData(".odp")]
    [InlineData(".odg")]
    [InlineData(".rtf")]
    [InlineData(".txt")]
    [InlineData(".vtt")]
    [InlineData(".srt")]
    public void OfficeAndCaptionFiles_AreAllowed(string extension)
    {
        // Arrange
        var media = new MediaOptions { AllowedFileExtensions = new HashSet<string>([".pdf"], StringComparer.OrdinalIgnoreCase) };

        // Act
        Configure(media, new LearningMediaOptions());

        // Assert
        Assert.Contains(extension, media.AllowedFileExtensions);
        Assert.Contains(".pdf", media.AllowedFileExtensions);
    }

    [Theory]
    [InlineData(".js")]
    [InlineData(".exe")]
    [InlineData(".aspx")]
    [InlineData(".svg")]
    [InlineData(".html")]
    [InlineData(".xlsm")]
    public void ExecutableOrScriptFiles_AreNeverAdded(string extension)
    {
        // Arrange
        var media = new MediaOptions { AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };

        // Act
        Configure(media, new LearningMediaOptions { AdditionalFileExtensions = [".ods", extension] });

        // Assert
        Assert.Contains(".ods", media.AllowedFileExtensions);
        Assert.DoesNotContain(extension, media.AllowedFileExtensions);
    }

    [Theory]
    [InlineData("mediafields/FileLesson/4abc/3f2504e0-4f89-11d3-9a0c-0305e82c3301_Safety plan.ods", null, null, "Safety plan.ods")]
    [InlineData("mediafields/FileLesson/4abc/3f2504e04f8911d39a0c0305e82c3301_Safety plan.ods", null, null, "Safety plan.ods")]
    [InlineData("mediafields/FileLesson/4abc/3F2504E04F8911D39A0C0305E82C3301-report.pdf", null, null, "report.pdf")]
    [InlineData("mediafields/FileLesson/4abc/1a2b3c4d.pdf", "Handbook 2026.pdf", null, "Handbook 2026.pdf")]
    [InlineData("mediafields/FileLesson/4abc/1a2b3c4d.pdf", "Handbook 2026.pdf", "The handbook", "The handbook")]
    [InlineData("courses/handbook.pdf", null, null, "handbook.pdf")]
    [InlineData("courses/handbook.pdf", "  ", " ", "handbook.pdf")]
    public void DisplayName_IsTheOriginalName(string path, string attachedName, string mediaText, string expected)
    {
        // Act
        var name = LearningFileNames.GetDisplayName(path, attachedName, mediaText);

        // Assert
        Assert.Equal(expected, name);
    }

    private static void Configure(MediaOptions media, LearningMediaOptions learning)
        => new LearningMediaOptionsSetup(Options.Create(learning)).PostConfigure(Options.DefaultName, media);
}
