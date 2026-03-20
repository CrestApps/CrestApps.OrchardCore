using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

internal static class BrowserNavigationPathParser
{
    public static string[] Split(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return Regex.Split(path, @"\s*(?:>>|>|/|\\|→|»)\s*")
            .Select(NormalizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
    }

    public static string NormalizeSegment(string value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
