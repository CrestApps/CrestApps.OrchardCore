using System.Runtime.InteropServices;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// Locates the native Copilot CLI runtime that the Copilot SDK spawns.
/// </summary>
/// <remarks>
/// The SDK ships an MSBuild target that places the CLI at <c>runtimes/{rid}/native/</c> in the build output.
/// That target is skipped when the CLI download is disabled, when a build produces a different runtime
/// identifier than the host, or when a publish profile trims native assets. In each of those cases the module
/// still loads and its settings still validate, so without an explicit probe the failure only surfaces when a
/// user starts a chat. This locator lets availability be reported honestly instead.
/// </remarks>
internal static class CopilotRuntimeLocator
{
    /// <summary>
    /// Determines whether the Copilot CLI runtime is present for the current runtime identifier.
    /// </summary>
    /// <param name="baseDirectory">The directory to probe. Defaults to the application base directory.</param>
    public static bool IsRuntimePresent(string baseDirectory = null)
        => GetRuntimePath(baseDirectory) is not null;

    /// <summary>
    /// Gets the full path of the Copilot CLI runtime, or <see langword="null"/> when it is not present.
    /// </summary>
    /// <param name="baseDirectory">The directory to probe. Defaults to the application base directory.</param>
    public static string GetRuntimePath(string baseDirectory = null)
    {
        var probeRoot = string.IsNullOrEmpty(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;

        var binary = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "copilot.exe"
            : "copilot";

        var ridPath = Path.Combine(probeRoot, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", binary);

        if (File.Exists(ridPath))
        {
            return ridPath;
        }

        // A self-contained or single-file publish flattens native assets next to the entry assembly.
        var flattenedPath = Path.Combine(probeRoot, binary);

        return File.Exists(flattenedPath)
            ? flattenedPath
            : null;
    }
}
