using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Holds the boundary that keeps a configuration entry's rules in one place.
/// </summary>
/// <remarks>
/// A configuration catalog entry can be written by an editor, by a recipe, by a deployment plan and by any service
/// that goes through its manager. Only the entry's handlers run on all of those paths, so a rule that lives in a
/// display driver is a rule the recipe path silently skips. These tests fail when that starts to happen again.
/// </remarks>
public class ValidationOwnershipArchitectureTests
{
    private static Dictionary<string, HashSet<string>> _stringConstants;

    private static readonly Regex _recipeStepRegistrationRegex = new(
        @"AddRecipeExecutionStep<\s*(?<step>\w+)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _recipeStepDeclarationRegex = new(
        @"class\s+(?<step>\w+)\s*:\s*NamedRecipeStepHandler",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _recipeStepEntityRegex = new(
        @"^\s*(?<entity>\w+)\s+entry\s*=\s*null;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex _handlerRegistrationRegex = new(
        @"ICatalogEntryHandler<\s*(?<entity>\w+)\s*>\s*,\s*(?<implementation>\w+)\s*>",
        RegexOptions.Compiled);

    private static readonly Regex _featureAttributeRegex = new(
        @"^\s*\[Feature\((?<argument>[^\)]+)\)\]",
        RegexOptions.Compiled);

    private static readonly Regex _typeDeclarationRegex = new(
        @"^(?:public |internal |sealed |abstract |static |partial )*(?:class|record|struct)\s+\w+",
        RegexOptions.Compiled);

    private static readonly Regex _constantDeclarationRegex = new(
        "\\bconst\\s+string\\s+(?<name>\\w+)\\s*=\\s*\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled);

    private static readonly Regex _stringLiteralRegex = new(
        "\"(?:[^\"\\\\]|\\\\.)*\"",
        RegexOptions.Compiled);

    private static readonly Regex _constantContainerRegex = new(
        @"^\s*(?:public |internal |sealed |abstract |static |partial )*(?:class|record|struct)\s+(?<name>\w+)",
        RegexOptions.Compiled);

    private static readonly Regex _displayDriverRegex = new(
        @"DisplayDriver<\s*(?<entity>\w+)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _managerWriteRegex = new(
        @"_\w*[Mm]anager\.(CreateAsync|UpdateAsync)\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _memberDeclarationRegex = new(
        @"^    (?:public|private|internal|protected)\s",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _displayManagerRegex = new(
        @"IDisplayManager<\s*(?<entity>\w+)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void ConfigurationCatalogEntries_AreDiscovered()
    {
        // Act
        var entities = GetConfigurationCatalogEntities();

        // Assert
        Assert.NotEmpty(entities);
        Assert.Contains("DialerProfile", entities);
        Assert.Contains("SubjectFlowSettings", entities);
    }

    [Fact]
    public void DisplayDrivers_ForConfigurationCatalogEntries_DeclareNoBusinessRules()
    {
        // Arrange
        var entities = GetConfigurationCatalogEntities();
        var violations = new List<string>();

        // Act
        foreach (var driver in EnumerateSourceFiles("Drivers"))
        {
            var lines = File.ReadAllLines(driver.FullPath);

            var declaresAGovernedEntity = _displayDriverRegex
                .Matches(string.Join('\n', lines))
                .Any(match => entities.Contains(match.Groups["entity"].Value));

            if (!declaresAGovernedEntity)
            {
                continue;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains("AddModelError", StringComparison.Ordinal))
                {
                    violations.Add($"{driver.RelativePath}({index + 1}): {lines[index].Trim()}");
                }
            }
        }

        // Assert
        Assert.True(
            violations.Count == 0,
            Describe(
                violations,
                "A display driver declared a rule for a configuration catalog entry. Move the rule to the entry's ICatalogEntryHandler so a recipe and an editor enforce the same set."));
    }

    [Fact]
    public void EveryConfigurationCatalogEntry_HasItsHandlersInTheFeatureThatCarriesItsRecipeStep()
    {
        // Arrange
        var catalogs = GetConfigurationCatalogRegistrations();
        var handlers = GetHandlerRegistrations();
        var violations = new List<string>();

        // Act
        foreach (var catalog in catalogs)
        {
            var registeredForThisEntity = handlers
                .Where(handler => string.Equals(handler.Entity, catalog.Entity, StringComparison.Ordinal))
                .ToList();

            if (!registeredForThisEntity.Any(handler => string.Equals(handler.Feature, catalog.Feature, StringComparison.Ordinal)))
            {
                violations.Add($"{catalog.Entity}: its recipe step is registered by {catalog.Feature} but no ICatalogEntryHandler<{catalog.Entity}> is.");
            }

            foreach (var handler in registeredForThisEntity)
            {
                if (!string.Equals(handler.Feature, catalog.Feature, StringComparison.Ordinal))
                {
                    violations.Add($"{handler.DeclaredIn}: {handler.Implementation} validates {catalog.Entity} but is registered by {handler.Feature}, while the recipe step is registered by {catalog.Feature}.");
                }
            }
        }

        // Assert
        Assert.True(catalogs.Count > 0, "No configuration catalog registration was inspected, so this gate proves nothing.");
        Assert.True(handlers.Count > 0, "No handler registration was inspected, so this gate proves nothing.");

        // Without these the gate would pass silently if feature resolution ever collapsed every registration onto one
        // identifier, or stopped resolving the constants that spell the identifiers, because the comparisons above
        // would then compare expressions rather than the features a tenant enables.
        Assert.True(
            catalogs.Select(catalog => catalog.Feature).Distinct(StringComparer.Ordinal).Count() > 1,
            "Every configuration catalog resolved to the same feature, so this gate can no longer tell features apart.");

        var unresolved = catalogs
            .Concat(handlers.Select(handler => new CatalogRegistration(handler.Entity, handler.Feature, handler.DeclaredIn)))
            .Where(registration => !registration.Feature.StartsWith("CrestApps.", StringComparison.Ordinal))
            .Select(registration => $"{registration.DeclaredIn}: '{registration.Feature}' is not a feature identifier.")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unresolved.Count == 0,
            Describe(
                unresolved,
                "A feature attribute was not resolved to the identifier a tenant enables, so this gate is comparing source expressions instead of features."));

        Assert.True(
            violations.Count == 0,
            Describe(
                violations,
                "A configuration catalog entry's rules are not registered by the feature that carries its recipe step, so a tenant that enables that feature alone imports the entry with those rules missing."));
    }

    [Fact]
    public void EveryConfigurationCatalogEditor_ValidatesThroughTheHandlersBeforeSaving()
    {
        // Arrange
        var entities = GetConfigurationCatalogEntities();
        var violations = new List<string>();
        var inspected = 0;

        // Act
        foreach (var controller in EnumerateSourceFiles("Controllers"))
        {
            var text = File.ReadAllText(controller.FullPath);

            var editsAConfigurationEntry = _displayManagerRegex
                .Matches(text)
                .Any(match => entities.Contains(match.Groups["entity"].Value));

            if (!editsAConfigurationEntry)
            {
                continue;
            }

            inspected++;

            // The unit is the action, not the file: one action can create or update depending on what it was given,
            // and both branches are covered by the single validation that precedes them.
            foreach (var (name, body) in EnumerateMembers(text))
            {
                var writesThroughAManager = _managerWriteRegex.IsMatch(body);
                var updatesAnEditor = body.Contains("UpdateEditorAsync(", StringComparison.Ordinal);

                if (!writesThroughAManager && !updatesAnEditor)
                {
                    continue;
                }

                if (!body.Contains("CatalogEntryValidation.ValidateAsync(", StringComparison.Ordinal))
                {
                    violations.Add($"{controller.RelativePath}: {name} writes a configuration catalog entry without running its handler rules.");
                }
            }
        }

        // Assert
        Assert.True(inspected > 0, "No configuration catalog editor was inspected, so this gate proves nothing.");

        Assert.True(
            violations.Count == 0,
            Describe(
                violations,
                "An admin editor saved a configuration catalog entry without running the entry's handler rules. Call CatalogEntryValidation.ValidateAsync after UpdateEditorAsync and before the ModelState check."));
    }

    /// <summary>
    /// Splits a type's source into its members.
    /// </summary>
    /// <remarks>
    /// The split is textual and deliberately coarse. It only has to keep one action's statements together and apart
    /// from the next action's, which member declarations at the type's own indentation already do.
    /// </remarks>
    /// <param name="text">The full source text of the file.</param>
    /// <returns>Each member's declaration line paired with the source that follows it.</returns>
    private static List<(string Name, string Body)> EnumerateMembers(string text)
    {
        var members = new List<(string Name, string Body)>();
        var name = "<type>";
        var body = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            if (_memberDeclarationRegex.IsMatch(line))
            {
                members.Add((name, body.ToString()));

                name = line.Trim();
                body.Clear();
            }

            body.AppendLine(line);
        }

        members.Add((name, body.ToString()));

        return members;
    }

    private static string Describe(List<string> violations, string guidance)
    {
        if (violations.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        builder.AppendLine(guidance);

        foreach (var violation in violations)
        {
            builder.AppendLine(violation);
        }

        return builder.ToString();
    }

    private static HashSet<string> GetConfigurationCatalogEntities()
    {
        return GetConfigurationCatalogRegistrations()
            .Select(registration => registration.Entity)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds every entity a recipe step imports as configuration, together with the feature that registers that step.
    /// </summary>
    /// <remarks>
    /// The governed set is derived from the registrations rather than from a list someone maintains, so adding a new
    /// configuration recipe step brings its entity under these gates without any further step. Runtime aggregates are
    /// outside the set by construction, because no recipe authors them.
    /// </remarks>
    /// <returns>The registered entity names paired with the feature and source file that register each of them.</returns>
    private static List<CatalogRegistration> GetConfigurationCatalogRegistrations()
    {
        var entitiesByStep = GetConfigurationEntitiesByRecipeStep();
        var registrations = new List<CatalogRegistration>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in EnumerateApplicationFiles("*.cs"))
        {
            foreach (var (line, feature) in EnumerateFeatureScopedLines(file))
            {
                foreach (var match in _recipeStepRegistrationRegex.Matches(line).Cast<Match>())
                {
                    if (!entitiesByStep.TryGetValue(match.Groups["step"].Value, out var entity))
                    {
                        continue;
                    }

                    if (seen.Add($"{file.RelativePath}|{feature}|{entity}"))
                    {
                        registrations.Add(new CatalogRegistration(entity, feature, file.RelativePath));
                    }
                }
            }
        }

        return registrations;
    }

    /// <summary>
    /// Maps each configuration recipe step to the entity it imports.
    /// </summary>
    /// <remarks>
    /// A configuration step declares the entry it is about to create or update before it looks the entry up, which is
    /// what separates it from a step that scripts an operation rather than a catalog. Reading the declaration keeps the
    /// mapping derived from the step itself instead of from a list that has to be kept in sync by hand.
    /// </remarks>
    /// <returns>The entity name imported by each recipe step handler, keyed by the handler's type name.</returns>
    private static Dictionary<string, string> GetConfigurationEntitiesByRecipeStep()
    {
        var entitiesByStep = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in EnumerateApplicationFiles("*.cs"))
        {
            var text = File.ReadAllText(file.FullPath);
            var declaration = _recipeStepDeclarationRegex.Match(text);

            if (!declaration.Success)
            {
                continue;
            }

            var entity = _recipeStepEntityRegex.Match(text);

            if (!entity.Success)
            {
                continue;
            }

            entitiesByStep[declaration.Groups["step"].Value] = entity.Groups["entity"].Value;
        }

        return entitiesByStep;
    }

    /// <summary>
    /// Finds every catalog entry handler registration, together with the feature that registers it.
    /// </summary>
    /// <returns>The handled entity names paired with the implementation, feature and file that register each of them.</returns>
    private static List<HandlerRegistration> GetHandlerRegistrations()
    {
        var registrations = new List<HandlerRegistration>();

        foreach (var file in EnumerateApplicationFiles("*.cs"))
        {
            foreach (var (line, feature) in EnumerateFeatureScopedLines(file))
            {
                foreach (var match in _handlerRegistrationRegex.Matches(line).Cast<Match>())
                {
                    registrations.Add(new HandlerRegistration(
                        match.Groups["entity"].Value,
                        match.Groups["implementation"].Value,
                        feature,
                        file.RelativePath));
                }
            }
        }

        return registrations;
    }

    /// <summary>
    /// Reads a source file and pairs each line with the feature that owns the type the line sits in.
    /// </summary>
    /// <remarks>
    /// A startup class without a <c>Feature</c> attribute belongs to its module's default feature, which carries the
    /// module identifier. Resolving the attribute argument through the repository's own constants means the gate
    /// compares the feature identifiers a tenant actually enables rather than the expressions used to spell them.
    /// </remarks>
    /// <param name="file">The source file to read.</param>
    /// <returns>Each line of the file paired with its owning feature identifier.</returns>
    private static IEnumerable<(string Line, string Feature)> EnumerateFeatureScopedLines(SourceFile file)
    {
        var defaultFeature = GetProjectName(file.RelativePath);
        var currentFeature = defaultFeature;
        string pendingFeature = null;

        foreach (var line in File.ReadAllLines(file.FullPath))
        {
            var featureAttribute = _featureAttributeRegex.Match(line);

            if (featureAttribute.Success)
            {
                pendingFeature = ResolveFeatureIdentifier(featureAttribute.Groups["argument"].Value);

                continue;
            }

            if (_typeDeclarationRegex.IsMatch(line))
            {
                currentFeature = pendingFeature ?? defaultFeature;
                pendingFeature = null;
            }

            yield return (line, currentFeature);
        }
    }

    /// <summary>
    /// Reads the project folder out of a repository relative path.
    /// </summary>
    /// <param name="relativePath">The repository relative path of a source file.</param>
    /// <returns>The name of the project that owns the file, which is also its module's default feature identifier.</returns>
    private static string GetProjectName(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 2
            ? segments[2]
            : relativePath;
    }

    /// <summary>
    /// Turns the argument of a feature attribute into the feature identifier a tenant enables.
    /// </summary>
    /// <remarks>
    /// Constants are matched on their full nesting path, because names such as <c>Feature.Area</c> are declared by ten
    /// different constants classes with ten different values. An expression that still matches more than one value is
    /// left unresolved rather than guessed, and two different expressions then stay distinct, which is what the gate
    /// needs: guessing would let a catalog and a handler in genuinely different features compare equal.
    /// </remarks>
    /// <param name="argument">The source text of the attribute argument.</param>
    /// <returns>The resolved feature identifier, or the argument text when no single constant declares it.</returns>
    private static string ResolveFeatureIdentifier(string argument)
    {
        var text = argument.Trim();

        if (text.Length > 1 && text.StartsWith('"') && text.EndsWith('"'))
        {
            return text.Trim('"');
        }

        if (GetStringConstants().TryGetValue(text, out var values) && values.Count == 1)
        {
            return values.First();
        }

        return text;
    }

    /// <summary>
    /// Collects the string constants the repository declares, keyed by every suffix of their full nesting path.
    /// </summary>
    /// <returns>A map from a dotted name to the distinct values declared under it.</returns>
    private static Dictionary<string, HashSet<string>> GetStringConstants()
    {
        if (_stringConstants is not null)
        {
            return _stringConstants;
        }

        var constants = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in EnumerateApplicationFiles("*.cs"))
        {
            var scope = new List<string>();
            var scopeDepths = new List<int>();
            var depth = 0;
            string pendingType = null;

            foreach (var line in File.ReadAllLines(file.FullPath))
            {
                var containerMatch = _constantContainerRegex.Match(line);

                if (containerMatch.Success)
                {
                    pendingType = containerMatch.Groups["name"].Value;
                }

                var constant = _constantDeclarationRegex.Match(line);

                if (constant.Success && scope.Count > 0)
                {
                    RegisterConstant(constants, scope, constant.Groups["name"].Value, constant.Groups["value"].Value);
                }

                // Values such as route templates carry braces of their own, so literals cannot take part in the count
                // that tracks which type a constant is nested in.
                foreach (var character in _stringLiteralRegex.Replace(line, string.Empty))
                {
                    if (character == '{')
                    {
                        if (pendingType is not null)
                        {
                            scope.Add(pendingType);
                            scopeDepths.Add(depth);
                            pendingType = null;
                        }

                        depth++;
                    }
                    else if (character == '}')
                    {
                        depth--;

                        if (scopeDepths.Count > 0 && scopeDepths[scopeDepths.Count - 1] == depth)
                        {
                            scope.RemoveAt(scope.Count - 1);
                            scopeDepths.RemoveAt(scopeDepths.Count - 1);
                        }
                    }
                }
            }
        }

        _stringConstants = constants;

        return constants;
    }

    private static void RegisterConstant(
        Dictionary<string, HashSet<string>> constants,
        List<string> scope,
        string name,
        string value)
    {
        var segments = new List<string>(scope)
        {
            name,
        };

        for (var index = 0; index < segments.Count; index++)
        {
            var key = string.Join('.', segments.GetRange(index, segments.Count - index));

            if (!constants.TryGetValue(key, out var values))
            {
                values = new HashSet<string>(StringComparer.Ordinal);
                constants[key] = values;
            }

            values.Add(value);
        }
    }

    private static List<SourceFile> EnumerateSourceFiles(string folderName)
    {
        var separator = $"{Path.DirectorySeparatorChar}{folderName}{Path.DirectorySeparatorChar}";

        var files = EnumerateApplicationFiles("*.cs")
            .Where(file => file.FullPath.Contains(separator, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(files);

        return files;
    }

    private static List<SourceFile> EnumerateApplicationFiles(string searchPattern)
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var files = new List<SourceFile>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, searchPattern, SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(new SourceFile(file, Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/')));
        }

        return files;
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

    private sealed record SourceFile(string FullPath, string RelativePath);

    private sealed record CatalogRegistration(string Entity, string Feature, string DeclaredIn);

    private sealed record HandlerRegistration(string Entity, string Implementation, string Feature, string DeclaredIn);
}
