using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A number input with a fractional step posts a value such as "24.5", and script posts numbers the same way. Without
/// <c>services.AddInvariantNumberInputs()</c> a tenant whose culture uses a decimal comma binds "24.5" as 245 (de-DE) or not
/// at all (fr-FR), and renders an <c>asp-for</c> number input empty. This guard fails when a module's views have such an
/// input, or its controllers take a floating-point action parameter, and none of the module's code registers the
/// invariant number handling.
/// </summary>
public sealed class InvariantNumberInputsArchitectureTests
{
    private static readonly Regex _inputTagRegex = new(@"<input\b[^>]*>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _numberTypeRegex = new(@"\btype\s*=\s*""number""", RegexOptions.Compiled);

    // A step of "any", a decimal such as "0.01", or a value computed in Razor lets the input carry a fraction.
    private static readonly Regex _fractionalStepRegex = new(@"\bstep\s*=\s*""(any|@[^""]*|\d*\.\d+)""", RegexOptions.Compiled);

    // A public action whose parameter list holds a decimal, double or float.
    private static readonly Regex _floatingPointActionRegex = new(
        @"\bpublic\s+(?:async\s+)?[\w<>\[\]?]+\s+\w+\s*\([^)]*\b(?:decimal|double|float)\??\s+\w+",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void ModulesThatPostFractionalNumbers_RegisterInvariantNumberInputs()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        var violations = new List<string>();
        var modulesThatPostNumbers = 0;

        // Act
        foreach (var moduleDirectory in Directory.EnumerateDirectories(modulesRoot))
        {
            var example = Directory.EnumerateFiles(moduleDirectory, "*.cshtml", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .FirstOrDefault(HasFractionalNumberInput)
                ?? Directory.EnumerateFiles(moduleDirectory, "*Controller.cs", SearchOption.AllDirectories)
                    .Where(path => !IsBuildOutput(path))
                    .FirstOrDefault(path => _floatingPointActionRegex.IsMatch(File.ReadAllText(path)));

            if (example is null)
            {
                continue;
            }

            modulesThatPostNumbers++;

            var registers = Directory.EnumerateFiles(moduleDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Any(path => File.ReadAllText(path).Contains("AddInvariantNumberInputs()", StringComparison.Ordinal));

            if (!registers)
            {
                violations.Add($"{Path.GetFileName(moduleDirectory)} (for example {Path.GetRelativePath(repositoryRoot, example).Replace(Path.DirectorySeparatorChar, '/')})");
            }
        }

        // Assert
        Assert.True(
            modulesThatPostNumbers > 0,
            "No module posts a fractional number, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These modules post fractional numbers but never call services.AddInvariantNumberInputs() " +
            "(CrestApps.OrchardCore.Core), so in a decimal-comma culture \"24.5\" binds as 245 and asp-for number inputs " +
            $"render empty. Call it from the module's Startup: {string.Join(", ", violations)}.");
    }

    private static bool HasFractionalNumberInput(string viewPath)
        => _inputTagRegex.Matches(File.ReadAllText(viewPath))
            .Any(match => _numberTypeRegex.IsMatch(match.Value) && _fractionalStepRegex.IsMatch(match.Value));

    private static bool IsBuildOutput(string fullPath)
    {
        var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
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
