using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the workflow files against a YAML folding defect that is invisible during review and only
/// surfaces when the affected job runs.
/// </summary>
public sealed class WorkflowFoldedScalarTests
{
    /// <summary>
    /// A whitespace-only line inside a folded block scalar (<c>run: &gt;-</c>) is treated as a blank line, and
    /// YAML folds a blank line into a real newline instead of a space. That silently splits one shell command
    /// into two, so a continued argument such as a project path is executed as its own command and the job
    /// fails with a permission error rather than anything that points at the cause.
    /// </summary>
    [Fact]
    public void FoldedRunBlocks_DoNotContainBlankLines()
    {
        // Arrange
        var workflowDirectory = Path.Combine(FindRepositoryRoot(), ".github", "workflows");

        Assert.True(Directory.Exists(workflowDirectory), $"The workflow directory was not found at '{workflowDirectory}'.");

        var offenders = new List<string>();

        // Act
        foreach (var file in Directory.EnumerateFiles(workflowDirectory, "*.yml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            offenders.AddRange(FindBlankLinesInsideFoldedScalars(file));
        }

        // Assert
        Assert.True(
            offenders.Count == 0,
            "A folded block scalar contains a blank or whitespace-only line, which YAML folds into a newline and " +
            "splits the command in two. Remove the blank line: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> FindBlankLinesInsideFoldedScalars(string path)
    {
        var lines = File.ReadAllLines(path);
        var fileName = Path.GetFileName(path);
        var blockIndent = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (blockIndent < 0)
            {
                var trimmed = line.TrimEnd();

                if (IsFoldedRunHeader(trimmed))
                {
                    blockIndent = IndentOf(line);
                }

                continue;
            }

            if (line.Trim().Length == 0)
            {
                // A trailing blank line only ends the block, so it is a defect solely when more folded content follows.
                var next = FindNextContentLine(lines, i + 1);

                if (next >= 0 && IndentOf(lines[next]) > blockIndent)
                {
                    yield return $"{fileName}:{i + 1}";
                }

                continue;
            }

            if (IndentOf(line) <= blockIndent)
            {
                blockIndent = -1;
                i--;
            }
        }
    }

    private static bool IsFoldedRunHeader(string trimmed)
    {
        if (!trimmed.EndsWith(">-", StringComparison.Ordinal) &&
            !trimmed.EndsWith('>') &&
            !trimmed.EndsWith(">+", StringComparison.Ordinal))
        {
            return false;
        }

        var key = trimmed.TrimStart();

        if (key.StartsWith("- ", StringComparison.Ordinal))
        {
            key = key.Substring(2).TrimStart();
        }

        // Only `run:` is checked. Folded scalars elsewhere, such as an issue comment body, use blank lines
        // deliberately to produce paragraph breaks, and a newline there is harmless.
        return key.StartsWith("run:", StringComparison.Ordinal);
    }

    private static int FindNextContentLine(string[] lines, int start)
    {
        for (var i = start; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndentOf(string line)
    {
        var index = 0;

        while (index < line.Length && line[index] == ' ')
        {
            index++;
        }

        return index;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
