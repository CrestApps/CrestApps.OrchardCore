using System.Reflection;
using NuGet.Versioning;
using OrchardCore;

namespace CrestApps.OrchardCore;

/// <summary>
/// Provides helper methods for working with Orchard Core versioning and feature toggles.
/// </summary>
public static class OrchardCoreHelpers
{
    /// <summary>
    /// Determines whether the legacy format should be used based on the application context switch and version.
    /// The legacy format is enabled if the "LegacyAdminMenuNavigation" switch is set and the current version is 3.0.0 or higher.
    /// </summary>
    /// <returns>True if the legacy format should be used, otherwise false.</returns>
    public static bool UseLegacyAdminMenuFormat()
    {
        if (IsVersionIsLess(GetCurrentOrchardCoreVersion(), "3.0.0-preview-18490"))
        {
            // The legacy format was introduced in Orchard Core version 3.0.0-preview-18490.
            // Legacy format should always be used if the current version of Orchard Core is earlier than this.
            return true;
        }

        return AppContext.TryGetSwitch("LegacyAdminMenuNavigation", out var enable) && enable;
    }

    /// <summary>
    /// Stores the cached current version of the application to avoid redundant lookups.
    /// </summary>
    private static string _currentVersion;

    /// <summary>
    /// Retrieves the current version of Orchard Core from the assembly metadata.
    /// This value is cached for performance reasons.
    /// </summary>
    /// <returns>A string representing the current version of Orchard Core.</returns>
    public static string GetCurrentOrchardCoreVersion()
    {
        if (string.IsNullOrEmpty(_currentVersion))
        {
            var assembly = typeof(OrchardCoreConstants).GetTypeInfo().Assembly;

            _currentVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(_currentVersion))
            {
                _currentVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            }

            if (string.IsNullOrWhiteSpace(_currentVersion))
            {
                _currentVersion = assembly.GetName().Version?.ToString();
            }

            if (string.IsNullOrWhiteSpace(_currentVersion))
            {
                _currentVersion = "0.0.0";
            }
        }

        return _currentVersion;
    }

    /// <summary>
    /// Checks if the current Orchard Core version is greater than or equal to the specified minimum version.
    /// This method ensures that the current version meets or exceeds the required version.
    /// </summary>
    /// <param name="minVersion">The minimum required version.</param>
    /// <returns>True if the current version is greater than or equal to the specified version, otherwise false.</returns>
    public static bool IsOrchardCoreVersionGreaterOrEqual(string minVersion)
    {
        var currentVersion = GetCurrentOrchardCoreVersion();

        return IsVersionGreaterOrEqual(currentVersion, minVersion);
    }

    /// <summary>
    /// Compares two semantic version strings to determine if the current version is greater than or equal to the specified minimum version.
    /// This method handles versions with pre-release tags and normalizes incomplete versions.
    /// </summary>
    /// <param name="currentVersion">The current version string.</param>
    /// <param name="compareTo">The minimum required version string.</param>
    /// <returns>True if the current version is greater than or equal to the minimum version, otherwise false.</returns>
    public static bool IsVersionGreaterOrEqual(string currentVersion, string compareTo)
    {
        if (TryNormalizeSemanticVersion(currentVersion, out var semVerCurrent) &&
            TryNormalizeSemanticVersion(compareTo, out var semCompareTo))
        {
            return semVerCurrent >= semCompareTo;
        }

        return false;
    }

    /// <summary>
    /// Compares two semantic version strings to determine if the current version is less than to the specified minimum version.
    /// This method handles versions with pre-release tags and normalizes incomplete versions.
    /// </summary>
    /// <param name="currentVersion">The current version string.</param>
    /// <param name="compareTo">The minimum required version string.</param>
    /// <returns>True if the current version is less than to the minimum version, otherwise false.</returns>
    public static bool IsVersionIsLess(string currentVersion, string compareTo)
    {
        if (TryNormalizeSemanticVersion(currentVersion, out var semVerCurrent) &&
            TryNormalizeSemanticVersion(compareTo, out var semCompareTo))
        {
            return semVerCurrent < semCompareTo;
        }

        return false;
    }

    /// <summary>
    /// Attempts to parse and normalize a semantic version string.
    /// Ensures the version follows the "major.minor.patch" format and handles pre-release versions correctly.
    /// </summary>
    /// <param name="version">The version string to normalize and parse.</param>
    /// <param name="semVer">The resulting parsed semantic version.</param>
    /// <returns>True if parsing was successful, otherwise false.</returns>
    private static bool TryNormalizeSemanticVersion(string version, out SemanticVersion semVer)
    {
        semVer = null;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return SemanticVersion.TryParse(NormalizeVersionString(version), out semVer);
    }

    /// <summary>
    /// Normalizes a version string to ensure it follows the "major.minor.patch" format.
    /// If the version is missing minor or patch numbers, it appends ".0" as needed.
    /// Handles pre-release versions correctly by preserving the pre-release suffix.
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>A properly formatted semantic version string.</returns>
    private static string NormalizeVersionString(string version)
    {
        var preReleaseParts = version.Split('-', 2); // Separate pre-release suffix if present.
        var normalizedVersion = preReleaseParts[0].Split('+', 2)[0];
        var versionParts = normalizedVersion.Split('.');

        // Ensure at least "major.minor.patch" format
        if (versionParts.Length < 3)
        {
            normalizedVersion += string.Concat(Enumerable.Repeat(".0", 3 - versionParts.Length));
        }
        else if (versionParts.Length > 3)
        {
            normalizedVersion = string.Join(".", versionParts.Take(3));
        }

        if (preReleaseParts.Length == 2)
        {
            var metadataParts = preReleaseParts[1].Split('+', 2); // Separate metadata suffix if present.

            normalizedVersion += '-' + metadataParts[0];

            if (metadataParts.Length == 2)
            {
                normalizedVersion += '+' + metadataParts[1];
            }
        }
        else
        {
            var metadataParts = version.Split('+', 2); // Separate metadata suffix if present.

            if (metadataParts.Length == 2)
            {
                normalizedVersion += '+' + metadataParts[1];
            }
        }

        return normalizedVersion;
    }
}
