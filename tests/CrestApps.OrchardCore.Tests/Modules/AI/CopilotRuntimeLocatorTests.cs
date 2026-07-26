using CrestApps.OrchardCore.AI.Chat.Copilot.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

public sealed class CopilotRuntimeLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("copilot-runtime-probe").FullName;

    [Fact]
    public void IsRuntimePresent_WhenTheRuntimeIsMissing_ReturnsFalse()
    {
        // A build that skips the Copilot CLI download, a publish that trims native assets, or a runtime
        // identifier mismatch all leave the module loadable but unusable. The probe must report that.

        // Act
        var isPresent = CopilotRuntimeLocator.IsRuntimePresent(_root);

        // Assert
        Assert.False(isPresent);
        Assert.Null(CopilotRuntimeLocator.GetRuntimePath(_root));
    }

    [Fact]
    public void IsRuntimePresent_WhenTheRuntimeIsInTheRuntimeIdentifierFolder_ReturnsTrue()
    {
        // Arrange
        var expected = CreateRuntimeBinary(Path.Combine(
            _root,
            "runtimes",
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            "native"));

        // Act
        var isPresent = CopilotRuntimeLocator.IsRuntimePresent(_root);

        // Assert
        Assert.True(isPresent);
        Assert.Equal(expected, CopilotRuntimeLocator.GetRuntimePath(_root));
    }

    [Fact]
    public void IsRuntimePresent_WhenTheRuntimeIsFlattenedNextToTheEntryAssembly_ReturnsTrue()
    {
        // A self-contained or single-file publish flattens native assets, so the runtime-identifier folder does
        // not exist even though the CLI shipped.

        // Arrange
        var expected = CreateRuntimeBinary(_root);

        // Act
        var isPresent = CopilotRuntimeLocator.IsRuntimePresent(_root);

        // Assert
        Assert.True(isPresent);
        Assert.Equal(expected, CopilotRuntimeLocator.GetRuntimePath(_root));
    }

    [Fact]
    public void IsRuntimePresent_WhenAnotherRuntimeIdentifierIsPresent_ReturnsFalse()
    {
        // Building for a different runtime identifier than the host produces a binary the SDK cannot spawn.

        // Arrange
        CreateRuntimeBinary(Path.Combine(_root, "runtimes", "some-other-rid", "native"));

        // Act
        var isPresent = CopilotRuntimeLocator.IsRuntimePresent(_root);

        // Assert
        Assert.False(isPresent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string CreateRuntimeBinary(string directory)
    {
        Directory.CreateDirectory(directory);

        var binary = OperatingSystem.IsWindows()
            ? "copilot.exe"
            : "copilot";

        var path = Path.Combine(directory, binary);
        File.WriteAllText(path, string.Empty);

        return path;
    }
}
