using CrestApps.OrchardCore.Reports.Pdf.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Pdf.Services;

public sealed class ReportPdfFontResolverTests
{
    [Fact]
    public void Resolve_WhenWindowsFontsAreInstalled_ShouldReadTheirShortStyleSuffixes()
    {
        // Arrange
        var resolver = ReportPdfFontResolver.FromFiles(
        [
            "C:/Windows/Fonts/arial.ttf",
            "C:/Windows/Fonts/arialbd.ttf",
            "C:/Windows/Fonts/ariali.ttf",
            "C:/Windows/Fonts/arialbi.ttf",
            "C:/Windows/Fonts/segoeui.ttf",
            "C:/Windows/Fonts/segoeuib.ttf",
        ]);

        // Act
        var regular = resolver.Resolve("Arial", isBold: false);
        var bold = resolver.Resolve("Arial", isBold: true);
        var segoe = resolver.Resolve("Segoe UI", isBold: true);

        // Assert
        Assert.Equal("C:/Windows/Fonts/arial.ttf", regular.Path);
        Assert.Equal("C:/Windows/Fonts/arialbd.ttf", bold.Path);
        Assert.False(bold.SimulateBold);
        Assert.Equal("C:/Windows/Fonts/segoeuib.ttf", segoe.Path);
    }

    [Fact]
    public void Resolve_WhenArialIsNotInstalled_ShouldUseLiberationSans()
    {
        // Arrange
        var resolver = ReportPdfFontResolver.FromFiles(
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        ]);

        // Act
        var regular = resolver.Resolve("Arial", isBold: false);
        var bold = resolver.Resolve("Arial", isBold: true);

        // Assert
        Assert.Equal("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", regular.Path);
        Assert.Equal("/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf", bold.Path);
    }

    [Fact]
    public void Resolve_WhenTheFontHasNoBoldFace_ShouldAskForBoldToBeSimulated()
    {
        // Arrange
        var resolver = ReportPdfFontResolver.FromFiles(["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]);

        // Act
        var bold = resolver.Resolve("Arial", isBold: true);

        // Assert
        Assert.Equal("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", bold.Path);
        Assert.True(bold.SimulateBold);
    }

    [Fact]
    public void Resolve_WhenAShortSuffixHasNoFamilyBesideIt_ShouldReadTheWholeNameAsTheFamily()
    {
        // Arrange
        // "Credit" is not an italic "Cred", since no "Cred" font is installed.
        var resolver = ReportPdfFontResolver.FromFiles(["/fonts/Credit.ttf"]);

        // Act
        var regular = resolver.Resolve("Credit", isBold: false);

        // Assert
        Assert.Equal("/fonts/Credit.ttf", regular.Path);
    }

    [Fact]
    public void FromFiles_WhenNoFontIsInstalled_ShouldReturnNull()
    {
        // Act
        var resolver = ReportPdfFontResolver.FromFiles([]);

        // Assert
        Assert.Null(resolver);
    }
}
