using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A number input with a fractional step posts a value such as "24.5", and script posts numbers the same way. MVC reads a
/// form value in the request culture, so a tenant whose culture uses a decimal comma binds "24.5" as 245 (de-DE) or not at
/// all (fr-FR), and renders an <c>asp-for</c> number input empty. ASP.NET Core's own answer is the <c>__Invariant</c> form
/// field, which names the fields the framework reads invariantly:
/// <list type="bullet">
/// <item>an <c>asp-for</c> number input adds it by itself once the module calls <c>services.AddInvariantNumberInputs()</c>
/// (which turns on <c>FormInputRenderMode.DetectCultureFromInputType</c>);</item>
/// <item>a hand-written number input (<c>name="..."</c> without <c>asp-for</c>) is followed by
/// <c>&lt;input type="hidden" name="__Invariant" value="(the same name)" /&gt;</c>;</item>
/// <item>a floating-point parameter of a posted action is named in an <c>__Invariant</c> field of the module's views.</item>
/// </list>
/// These guards fail when one of them is missing.
/// </summary>
public sealed class InvariantNumberInputsArchitectureTests
{
    private static readonly Regex _inputTagRegex = new(@"<input\b[^>]*>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _numberTypeRegex = new(@"\btype\s*=\s*""number""", RegexOptions.Compiled);

    // A step of "any", a decimal such as "0.01", or a value computed in Razor lets the input carry a fraction.
    private static readonly Regex _fractionalStepRegex = new(@"\bstep\s*=\s*""(any|@[^""]*|\d*\.\d+)""", RegexOptions.Compiled);

    // The name attribute itself, not data-name or another attribute ending in "name". A Razor expression in it, such as
    // @(prefix + ".Amount") or @(percent ? "Percent" : "Amount"), may hold quotes of its own.
    private static readonly Regex _nameRegex = new(@"(?<![-\w])name\s*=\s*""((?:@\((?:[^()]|\([^()]*\))*\)|[^""@]|@(?!\())*)""", RegexOptions.Compiled);

    // A posted action whose parameter list holds a decimal, double or float.
    private static readonly Regex _postedFloatingPointActionRegex = new(
        @"\[Http(?:Post|Put)[^\]]*\][\s\S]{0,400}?\bpublic\s+(?:async\s+)?[\w<>\[\]?]+\s+\w+\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex _floatingPointParameterRegex = new(@"\b(?:decimal|double|float)\??\s+(\w+)", RegexOptions.Compiled);

    [Fact]
    public void ModulesWithFractionalAspForNumberInputs_RegisterInvariantNumberInputs()
    {
        // Arrange
        var violations = new List<string>();
        var modulesThatPostNumbers = 0;

        // Act
        foreach (var moduleDirectory in ModuleDirectories())
        {
            var example = Views(moduleDirectory).FirstOrDefault(path => NumberInputs(path).Any(tag => tag.Contains("asp-for", StringComparison.Ordinal)));

            if (example is null)
            {
                continue;
            }

            modulesThatPostNumbers++;

            var registers = Files(moduleDirectory, "*.cs")
                .Any(path => File.ReadAllText(path).Contains("AddInvariantNumberInputs()", StringComparison.Ordinal));

            if (!registers)
            {
                violations.Add($"{Path.GetFileName(moduleDirectory)} (for example {Relative(example)})");
            }
        }

        // Assert
        Assert.True(modulesThatPostNumbers > 0, "No module has a fractional asp-for number input, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These modules have fractional asp-for number inputs but never call services.AddInvariantNumberInputs() " +
            "(CrestApps.OrchardCore.Core), so in a decimal-comma culture \"24.5\" binds as 245 and the inputs render empty. " +
            $"Call it from the module's Startup: {string.Join(", ", violations)}.");
    }

    [Fact]
    public void HandWrittenFractionalNumberInputs_AreNamedInAnInvariantField()
    {
        // Arrange
        var violations = new List<string>();
        var handWritten = 0;

        // Act
        foreach (var moduleDirectory in ModuleDirectories())
        {
            foreach (var view in Views(moduleDirectory))
            {
                var text = File.ReadAllText(view);

                foreach (var tag in NumberInputs(view).Where(tag => !tag.Contains("asp-for", StringComparison.Ordinal)))
                {
                    var name = _nameRegex.Match(tag);

                    // An input with no name isn't posted (a script reads it).
                    if (!name.Success)
                    {
                        continue;
                    }

                    handWritten++;

                    if (!text.Contains($"<input type=\"hidden\" name=\"__Invariant\" value=\"{name.Groups[1].Value}\" />", StringComparison.Ordinal))
                    {
                        violations.Add($"{Relative(view)} ({name.Groups[1].Value})");
                    }
                }
            }
        }

        // Assert
        Assert.True(handWritten > 0, "No hand-written fractional number input was found, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These hand-written number inputs post a value with a dot but no __Invariant field names them, so in a " +
            "decimal-comma culture \"24.5\" binds as 245. Follow each with " +
            "<input type=\"hidden\" name=\"__Invariant\" value=\"(the same name)\" />, and write its value with " +
            $"CultureInfo.InvariantCulture: {string.Join(", ", violations)}.");
    }

    [Fact]
    public void PostedFloatingPointActionParameters_AreNamedInAnInvariantField()
    {
        // Arrange
        var violations = new List<string>();
        var parameters = 0;

        // Act
        foreach (var moduleDirectory in ModuleDirectories())
        {
            var views = string.Join('\n', Views(moduleDirectory).Select(File.ReadAllText));

            foreach (var controller in Files(moduleDirectory, "*Controller.cs"))
            {
                foreach (Match action in _postedFloatingPointActionRegex.Matches(File.ReadAllText(controller)))
                {
                    foreach (Match parameter in _floatingPointParameterRegex.Matches(action.Groups[1].Value))
                    {
                        parameters++;

                        if (!views.Contains($"name=\"__Invariant\" value=\"{parameter.Groups[1].Value}\"", StringComparison.Ordinal))
                        {
                            violations.Add($"{Relative(controller)} ({parameter.Groups[1].Value})");
                        }
                    }
                }
            }
        }

        // Assert
        Assert.True(parameters > 0, "No posted action takes a floating-point parameter, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These posted actions take a decimal, double or float that no __Invariant field of the module's views names, " +
            "so in a decimal-comma culture \"24.5\" binds as 245. Add <input type=\"hidden\" name=\"__Invariant\" " +
            $"value=\"(the parameter's name)\" /> to the form that posts it: {string.Join(", ", violations)}.");
    }

    private static IEnumerable<string> NumberInputs(string viewPath)
        => _inputTagRegex.Matches(File.ReadAllText(viewPath))
            .Select(match => match.Value)
            .Where(tag => _numberTypeRegex.IsMatch(tag) && _fractionalStepRegex.IsMatch(tag));

    private static IEnumerable<string> ModuleDirectories()
        => Directory.EnumerateDirectories(Path.Combine(RepositoryRoot, "src", "Modules"));

    private static IEnumerable<string> Views(string moduleDirectory)
        => Files(moduleDirectory, "*.cshtml");

    private static IEnumerable<string> Files(string directory, string pattern)
        => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).Where(path => !IsBuildOutput(path));

    private static string Relative(string path)
        => Path.GetRelativePath(RepositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');

    private static bool IsBuildOutput(string fullPath)
    {
        var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

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
