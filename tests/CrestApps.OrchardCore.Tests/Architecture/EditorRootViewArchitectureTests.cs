using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// An editor built with <c>IDisplayManager&lt;TModel&gt;.BuildEditorAsync</c> returns a <c>{TModel}_Edit</c> shape. When the
/// page renders that shape and no <c>{TModel}.Edit.cshtml</c> view exists, Orchard Core throws "The shape type
/// '{TModel}_Edit' is not found" and the edit page fails with a 500. Unit tests that build the editor with a stub
/// display never render the root shape, so they miss the gap. This guard fails when a display manager's editor is
/// built for a model that has no root edit view anywhere under <c>src</c>.
/// </summary>
public sealed class EditorRootViewArchitectureTests
{
    private static readonly Regex _displayManagerRegex = new(
        @"IDisplayManager<(?<model>\w+)>\s+(?<name>\w+)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> _skippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "node_modules",
        ".git",
    };

    // Editors whose pages render the shape's zones directly instead of the root shape, so they need no root view.
    private static readonly HashSet<string> _zoneRenderedEditors = new(StringComparer.Ordinal)
    {
        "ListContentTransferEntryOptions",
        "LocalDncListOptions",
    };

    [Fact]
    public void EveryEditorBuiltWithADisplayManager_HasARootEditView()
    {
        // Arrange
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var view in EnumerateFiles(sourceRoot, "*.cshtml"))
        {
            viewNames.Add(Path.GetFileName(view));
        }

        // Act
        foreach (var file in EnumerateFiles(sourceRoot, "*.cs"))
        {
            var text = File.ReadAllText(file);

            if (!text.Contains("BuildEditorAsync(", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in _displayManagerRegex.Matches(text))
            {
                var model = match.Groups["model"].Value;
                var name = match.Groups["name"].Value;

                if (model == "TModel" || _zoneRenderedEditors.Contains(model))
                {
                    continue;
                }

                if (!Regex.IsMatch(text, $@"\b{Regex.Escape(name)}\.BuildEditorAsync\("))
                {
                    continue;
                }

                if (!viewNames.Contains($"{model}.Edit.cshtml"))
                {
                    missing.Add($"{Path.GetRelativePath(sourceRoot, file)}: no {model}.Edit.cshtml for the {model}_Edit shape");
                }
            }
        }

        // Assert
        Assert.True(
            missing.Count == 0,
            "Add a root edit view (usually '@await DisplayAsync(Model.Content)') for these editors:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!_skippedDirectoryNames.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory, pattern))
            {
                yield return file;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
