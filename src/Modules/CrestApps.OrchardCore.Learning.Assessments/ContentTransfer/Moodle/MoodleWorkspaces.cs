using System.Collections.Concurrent;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;

/// <summary>
/// A Moodle backup extracted for one read of an import: its folder and what was read from it.
/// </summary>
/// <param name="Folder">The folder the backup was extracted to.</param>
/// <param name="Backup">The backup.</param>
internal sealed record MoodleWorkspace(string Folder, MoodleBackup Backup);

/// <summary>
/// The Moodle backups extracted by the file readers that are open, by a random token the reader writes in each of its
/// rows. A row's handler finds the backup's files (packages, documents) through the token while the reader is open; the
/// reader removes its backup, and deletes its folder, when it is disposed at the end of the read. A token a row of any
/// other file names finds nothing.
/// </summary>
internal sealed class MoodleWorkspaces
{
    private readonly ConcurrentDictionary<string, MoodleWorkspace> _workspaces = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    public MoodleWorkspaces(ILogger<MoodleWorkspaces> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds an extracted backup.
    /// </summary>
    /// <param name="workspace">The backup.</param>
    /// <returns>Its token.</returns>
    public string Add(MoodleWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var token = Guid.NewGuid().ToString("n");
        _workspaces[token] = workspace;

        return token;
    }

    /// <summary>
    /// Finds an extracted backup.
    /// </summary>
    /// <param name="token">Its token.</param>
    /// <returns>The backup, or <see langword="null"/>.</returns>
    public MoodleWorkspace Find(string token)
        => !string.IsNullOrEmpty(token) && _workspaces.TryGetValue(token, out var workspace) ? workspace : null;

    /// <summary>
    /// Removes an extracted backup and deletes its folder.
    /// </summary>
    /// <param name="token">Its token.</param>
    public void Remove(string token)
    {
        if (string.IsNullOrEmpty(token) || !_workspaces.TryRemove(token, out var workspace))
        {
            return;
        }

        Delete(workspace.Folder, _logger);
    }

    /// <summary>
    /// Deletes a folder the reader extracted to, logging a failure.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="logger">The logger.</param>
    public static void Delete(string folder, ILogger logger)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The folder of an extracted Moodle backup could not be deleted.");
        }
    }
}
