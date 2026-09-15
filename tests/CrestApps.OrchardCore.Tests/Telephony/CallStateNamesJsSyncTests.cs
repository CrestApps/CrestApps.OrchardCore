using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards that the shared browser client's <c>CALL_STATE_NAMES</c> array stays in ordinal sync with the
/// server-side <see cref="CallState"/> enum, so the C# enum remains authoritative for the wire ordinals the
/// soft phone and contact-center clients render.
/// </summary>
public sealed class CallStateNamesJsSyncTests
{
    [Fact]
    public void CallStateNames_InSharedTelephonyClientScript_MatchTheEnumOrdinalOrder()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.Telephony",
            "Assets",
            "js",
            "telephony-client.js");

        Assert.True(File.Exists(scriptPath), $"The shared telephony client script was not found at '{scriptPath}'.");

        var source = File.ReadAllText(scriptPath);

        // Act
        var jsStateNames = ExtractCallStateNames(source);
        var enumValues = Enum.GetValues<CallState>()
            .OrderBy(state => (int)state)
            .ToArray();
        var enumStateNames = enumValues
            .Select(state => state.ToString())
            .ToArray();

        // Assert
        Assert.Equal(enumStateNames, jsStateNames);

        // The shared script indexes CALL_STATE_NAMES by the numeric enum value, so the enum must be a
        // contiguous zero-based sequence with no gaps or aliases; otherwise a numeric state would map to
        // the wrong name at runtime while the name-order comparison above still passed.
        for (var index = 0; index < enumValues.Length; index++)
        {
            Assert.Equal(index, (int)enumValues[index]);
        }
    }

    private static string[] ExtractCallStateNames(string source)
    {
        var arrayMatch = Regex.Match(
            source,
            @"CALL_STATE_NAMES\s*=\s*\[(?<items>[^\]]*)\]",
            RegexOptions.Singleline);

        Assert.True(arrayMatch.Success, "Unable to locate the CALL_STATE_NAMES array in telephony-client.js.");

        return Regex.Matches(arrayMatch.Groups["items"].Value, @"'(?<name>[^']*)'")
            .Select(match => match.Groups["name"].Value)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
