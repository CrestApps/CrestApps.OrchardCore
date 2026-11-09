using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards that every recipe step and workflow activity a CrestApps module ships is described by a JSON schema, so
/// CrestApps Recipes (and the exported schema reference AI agents read) knows about it.
/// </summary>
/// <remarks>
/// Schemas are owned by the modules, and most modules are not referenced by this test project, so the guard reads the
/// source: it finds the recipe step handlers registered with <c>AddRecipeExecutionStep</c> (or as an
/// <c>IRecipeStepHandler</c>), the workflow activities, and the schema definitions, resolves the names they declare
/// (string literals, constants and <c>nameof</c>), and compares them.
/// </remarks>
public sealed partial class RecipeSchemaCoverageArchitectureTests
{
    private static readonly string[] _activityRoots = ["TaskActivity", "EventActivity", "Activity"];

    private static readonly string[] _activitySchemaRoots = ["WorkflowActivitySchemaDefinitionBase", "IWorkflowActivitySchemaDefinition"];

    private static readonly string[] _recipeStepSchemaRoots = ["IRecipeStep", "RecipeStepSchemaBase", "RecordsRecipeStepSchemaBase", "ContentDefinitionRecipeStepBase"];

    private static readonly Lazy<SourceIndex> _index = new(SourceIndex.Load);

    [Fact]
    public void EveryRecipeStepHandler_HasARecipeStepSchema()
    {
        var index = _index.Value;
        var schemaNames = index.GetRecipeStepSchemaNames().Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var handlers = index.GetRecipeStepHandlerNames().ToArray();

        // Guards the scan itself: steps registered in different ways must be found.
        Assert.Contains(handlers, entry => entry.Name == "Coupons");
        Assert.Contains(handlers, entry => entry.Name == "ContactCenterQueue");
        Assert.Contains(handlers, entry => entry.Name == "IndexUsers");

        var missing = handlers
            .Where(entry => !schemaNames.Contains(entry.Name))
            .Select(entry => $"{entry.Name} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "These recipe steps have no IRecipeStep schema:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryModuleOwnedRecipeStepSchema_DescribesARegisteredHandler()
    {
        var index = _index.Value;
        var handlerNames = index.GetRecipeStepHandlerNames().Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Recipes.Core also describes Orchard Core's own steps, so only the schemas the modules own are checked.
        var stale = index.GetRecipeStepSchemaNames()
            .Where(entry => !IsInRecipesCore(entry.File) && !handlerNames.Contains(entry.Name))
            .Select(entry => $"{entry.Name} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0, "These recipe step schemas describe no registered handler:" + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    [Fact]
    public void EveryWorkflowActivity_HasAnActivitySchema()
    {
        var index = _index.Value;
        var schemaNames = index.GetActivitySchemaNames().Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
        var activities = index.GetActivityNames().ToArray();

        // Guards the scan itself: activities deriving from intermediate bases, or overriding their name, must be found.
        Assert.Contains(activities, entry => entry.Name == "CreateCalendarEventTask");
        Assert.Contains(activities, entry => entry.Name == "WorkOrderCreated");
        Assert.Contains(activities, entry => entry.Name == "UpdateContactTask");

        var missing = activities
            .Where(entry => !schemaNames.Contains(entry.Name))
            .Select(entry => $"{entry.Name} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "These workflow activities have no IWorkflowActivitySchemaDefinition:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryModuleOwnedActivitySchema_DescribesAnExistingActivity()
    {
        var index = _index.Value;
        var activityNames = index.GetActivityNames().Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

        // Recipes.Core also describes Orchard Core's own activities, so only the schemas the modules own are checked.
        var stale = index.GetActivitySchemaNames()
            .Where(entry => !IsInRecipesCore(entry.File) && !activityNames.Contains(entry.Name))
            .Select(entry => $"{entry.Name} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0, "These activity schemas describe no workflow activity:" + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    [Fact]
    public void EveryActivitySchemaOwnedByAModule_IsRegistered()
    {
        var index = _index.Value;

        var unregistered = index.GetActivitySchemaNames()
            .Where(entry => !IsInRecipesCore(entry.File) && !index.IsRegistered(entry.ClassName, "AddWorkflowActivitySchema<"))
            .Select(entry => $"{entry.ClassName} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(unregistered.Length == 0, "These activity schemas are never registered with AddWorkflowActivitySchema:" + Environment.NewLine + string.Join(Environment.NewLine, unregistered));
    }

    [Fact]
    public void EveryRecipeStepSchemaOwnedByAModule_IsRegistered()
    {
        var index = _index.Value;

        var unregistered = index.GetRecipeStepSchemaNames()
            .Where(entry => !IsInRecipesCore(entry.File) && !index.IsRegistered(entry.ClassName, "IRecipeStep,"))
            .Select(entry => $"{entry.ClassName} ({entry.File})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(unregistered.Length == 0, "These recipe step schemas are never registered as an IRecipeStep:" + Environment.NewLine + string.Join(Environment.NewLine, unregistered));
    }

    private static bool IsInRecipesCore(string file)
        => file.Replace('\\', '/').Contains("/CrestApps.OrchardCore.Recipes.Core/", StringComparison.Ordinal);

    [GeneratedRegex(@"(?<modifiers>(?:\b(?:public|internal|private|protected|sealed|abstract|static|partial|file)\s+)*)\bclass\s+(?<name>\w+)(?:\s*<[^>{]*>)?(?:\s*\((?<ctor>[^)]*)\))?\s*(?::\s*(?<bases>[^{;]+))?[{;]", RegexOptions.Singleline)]
    private static partial Regex ClassRegex();

    [GeneratedRegex(@"\b(?<name>\w+)\s*=\s*""(?<value>[^""]*)""\s*;")]
    private static partial Regex StringConstantRegex();

    [GeneratedRegex(@"AddRecipeExecutionStep<(?<type>[\w.]+)>")]
    private static partial Regex RecipeExecutionStepRegex();

    [GeneratedRegex(@"AddScoped<IRecipeStepHandler,\s*(?<type>[\w.]+)>")]
    private static partial Regex RecipeStepHandlerRegistrationRegex();

    [GeneratedRegex(@":\s*base\(\s*(?<token>[^,)]+)")]
    private static partial Regex BaseCallRegex();

    [GeneratedRegex(@"\bstring\s+Name\s*(?:=>\s*(?<arrow>[^;]+)|\{\s*get;\s*\}\s*=\s*(?<init>[^;]+)|\{\s*get\s*=>\s*(?<getter>[^;]+);\s*\})")]
    private static partial Regex NamePropertyRegex();

    private sealed record NamedEntry(string Name, string ClassName, string File);

    private sealed record ClassDeclaration(string Name, bool IsAbstract, string FirstBase, string AllBases, string Body, string File);

    private sealed class SourceIndex
    {
        private readonly Dictionary<string, string> _sources = [];
        private readonly List<ClassDeclaration> _classes = [];
        private readonly HashSet<string> _classNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(string File, string Value)>> _constants = new(StringComparer.Ordinal);

        public static SourceIndex Load()
        {
            var index = new SourceIndex();
            var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = file.Replace('\\', '/');

                if (normalized.Contains("/bin/", StringComparison.Ordinal) || normalized.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
                var text = StripComments(File.ReadAllText(file));
                index._sources[relative] = text;
                index.IndexClasses(relative, text);

                foreach (Match match in StringConstantRegex().Matches(text))
                {
                    var name = match.Groups["name"].Value;

                    if (!index._constants.TryGetValue(name, out var values))
                    {
                        index._constants[name] = values = [];
                    }

                    values.Add((relative, match.Groups["value"].Value));
                }
            }

            return index;
        }

        public IEnumerable<NamedEntry> GetRecipeStepHandlerNames()
        {
            var types = new HashSet<string>(StringComparer.Ordinal);

            foreach (var text in _sources.Values)
            {
                foreach (Match match in RecipeExecutionStepRegex().Matches(text))
                {
                    types.Add(match.Groups["type"].Value.Split('.')[^1]);
                }

                foreach (Match match in RecipeStepHandlerRegistrationRegex().Matches(text))
                {
                    types.Add(match.Groups["type"].Value.Split('.')[^1]);
                }
            }

            foreach (var type in types.Order(StringComparer.Ordinal))
            {
                var declaration = _classes.FirstOrDefault(candidate => candidate.Name == type && !candidate.IsAbstract);

                // A plain IRecipeStepHandler that observes other steps (for example after ContentDefinition) has no
                // step name of its own.
                if (declaration is null || !InheritsFrom(declaration, ["NamedRecipeStepHandler"]))
                {
                    continue;
                }

                var match = BaseCallRegex().Match(declaration.Body);

                if (!match.Success)
                {
                    // A primary constructor passes the name in the base list instead.
                    match = Regex.Match(declaration.AllBases, @"\((?<token>[^,)]+)");
                }

                var name = match.Success ? Resolve(match.Groups["token"].Value, declaration.File, declaration.Body) : null;

                // An unresolved name is reported as missing, naming the class, so the resolver can be taught the pattern.
                name ??= $"<unresolved step name of {type}>";

                yield return new NamedEntry(name, type, declaration.File);
            }
        }

        public IEnumerable<NamedEntry> GetRecipeStepSchemaNames()
            => GetNamedClasses(_recipeStepSchemaRoots, fallbackToClassName: false);

        public IEnumerable<NamedEntry> GetActivitySchemaNames()
            => GetNamedClasses(_activitySchemaRoots, fallbackToClassName: false);

        public IEnumerable<NamedEntry> GetActivityNames()
            => GetNamedClasses(_activityRoots, fallbackToClassName: true);

        public bool IsRegistered(string className, string registrationMarker)
            => _sources.Values.Any(text =>
                text.Contains(registrationMarker, StringComparison.Ordinal) &&
                Regex.IsMatch(text, $@"(?:{Regex.Escape(registrationMarker)}\s*{Regex.Escape(className)}\s*>|{Regex.Escape(registrationMarker)}\s*[\w.]*\.{Regex.Escape(className)}\s*>)"));

        private IEnumerable<NamedEntry> GetNamedClasses(string[] roots, bool fallbackToClassName)
        {
            foreach (var declaration in _classes.Where(candidate => !candidate.IsAbstract && InheritsFrom(candidate, roots)))
            {
                var match = NamePropertyRegex().Match(declaration.Body);
                string name = null;

                if (match.Success)
                {
                    var token = match.Groups["arrow"].Success
                        ? match.Groups["arrow"].Value
                        : match.Groups["init"].Success ? match.Groups["init"].Value : match.Groups["getter"].Value;

                    name = Resolve(token, declaration.File, declaration.Body);
                }
                else if (fallbackToClassName)
                {
                    name = declaration.Name;
                }

                name ??= $"<unresolved name of {declaration.Name}>";

                yield return new NamedEntry(name, declaration.Name, declaration.File);
            }
        }

        private bool InheritsFrom(ClassDeclaration declaration, string[] roots)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = declaration;

            while (current is not null && visited.Add(current.Name))
            {
                // Interfaces can appear anywhere in the base list; a base class only first.
                if (roots.Any(root => root.StartsWith('I') && char.IsUpper(root[1])
                    ? Regex.IsMatch(current.AllBases, $@"\b{root}\b(?!<)")
                    : current.FirstBase == root))
                {
                    return true;
                }

                current = _classes.FirstOrDefault(candidate => candidate.Name == current.FirstBase);
            }

            return false;
        }

        private string Resolve(string token, string file, string scope = null)
        {
            token = token.Trim();

            if (token.StartsWith('"') && token.EndsWith('"'))
            {
                return token[1..^1];
            }

            var nameOf = Regex.Match(token, @"^nameof\((?<target>[\w.<>]+)\)$");

            if (nameOf.Success)
            {
                return nameOf.Groups["target"].Value.Split('.')[^1];
            }

            if (!Regex.IsMatch(token, @"^[\w.]+$"))
            {
                return null;
            }

            var segments = token.Split('.');
            var member = segments[^1];

            if (segments.Length == 1)
            {
                return (scope is null ? null : FindMemberValue(scope, member, file))
                    ?? FindMemberValue(_sources[file], member, file)
                    ?? (_constants.TryGetValue(member, out var candidates)
                        ? candidates.Select(candidate => candidate.Value).Distinct().SingleOrDefault()
                        : null);
            }

            var values = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (sourceFile, text) in _sources)
            {
                // Narrow to the owning class, then to each nested class (CustomersConstants.Recipes.Customers). A leading
                // segment that names no class in the file is a namespace qualifier and is skipped.
                IEnumerable<string> bodies = [text];
                var narrowed = false;

                foreach (var owner in segments[..^1])
                {
                    var next = bodies.SelectMany(body => GetClassBodies(body, owner)).ToArray();

                    if (next.Length == 0 && !narrowed && !_classNames.Contains(owner))
                    {
                        continue;
                    }

                    bodies = next;
                    narrowed = true;
                }

                if (!narrowed)
                {
                    continue;
                }

                foreach (var body in bodies)
                {
                    var value = FindMemberValue(body, member, sourceFile);

                    if (value is not null)
                    {
                        values.Add(value);
                    }
                }
            }

            return values.Count == 1 ? values.Single() : null;
        }

        private string FindMemberValue(string text, string member, string file)
        {
            var match = Regex.Match(text, $@"\b{Regex.Escape(member)}\s*(?:=>|=)\s*(?:""(?<value>[^""]*)""|nameof\((?<nameof>[\w.]+)\)|(?<token>[\w.]+))\s*;");

            if (!match.Success)
            {
                return null;
            }

            if (match.Groups["value"].Success)
            {
                return match.Groups["value"].Value;
            }

            if (match.Groups["nameof"].Success)
            {
                return match.Groups["nameof"].Value.Split('.')[^1];
            }

            var token = match.Groups["token"].Value;

            return token == member ? null : Resolve(token, file);
        }

        private static IEnumerable<string> GetClassBodies(string text, string className)
        {
            foreach (Match match in Regex.Matches(text, $@"\bclass\s+{Regex.Escape(className)}\b[^{{;]*\{{"))
            {
                var start = match.Index + match.Length;
                var depth = 1;
                var index = start;

                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '{')
                    {
                        depth++;
                    }
                    else if (text[index] == '}')
                    {
                        depth--;
                    }

                    index++;
                }

                yield return text[start..Math.Max(start, index - 1)];
            }
        }

        private void IndexClasses(string file, string text)
        {
            var matches = ClassRegex().Matches(text);

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var bases = match.Groups["bases"].Success ? match.Groups["bases"].Value : string.Empty;
                var whereIndex = bases.IndexOf(" where ", StringComparison.Ordinal);

                if (whereIndex >= 0)
                {
                    bases = bases[..whereIndex];
                }

                var firstBase = SplitTopLevel(bases).FirstOrDefault()?.Trim() ?? string.Empty;
                firstBase = Regex.Replace(firstBase, @"\s*[<(].*$", string.Empty, RegexOptions.Singleline).Split('.')[^1];

                _classNames.Add(match.Groups["name"].Value);
                _classes.Add(new ClassDeclaration(
                    match.Groups["name"].Value,
                    match.Groups["modifiers"].Value.Contains("abstract", StringComparison.Ordinal) || match.Groups["modifiers"].Value.Contains("static", StringComparison.Ordinal),
                    firstBase,
                    bases,
                    text[match.Index..end],
                    file));
            }
        }

        private static IEnumerable<string> SplitTopLevel(string bases)
        {
            var depth = 0;
            var start = 0;

            for (var i = 0; i < bases.Length; i++)
            {
                switch (bases[i])
                {
                    case '<' or '(':
                        depth++;
                        break;
                    case '>' or ')':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        yield return bases[start..i];
                        start = i + 1;
                        break;
                }
            }

            yield return bases[start..];
        }

        private static string StripComments(string text)
            => Regex.Replace(text, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);
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
