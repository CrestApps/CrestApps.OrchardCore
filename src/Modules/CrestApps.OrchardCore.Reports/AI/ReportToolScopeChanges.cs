using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// The designed reports, views and groups the Report Builder tools changed in the current scope. Every tool call of one
/// chat message runs in the same scope, and the report, view and group catalogs read from the cached document, which
/// is refreshed only once the scope commits. A report changed or created earlier in the message would be read as it
/// was before, and saving it again would drop the first change, so the tools refuse to touch it again until the next
/// reply.
/// </summary>
public sealed class ReportToolScopeChanges
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the answer to a tool call about a report, view or group changed earlier in the same message.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <returns>The answer.</returns>
    public static string ChangedMessage(string id)
        => $"'{id}' was changed earlier in this message. Its change is saved when this reply ends; do the next step on it in your next reply.";

    /// <summary>
    /// Gets a value indicating whether anything was changed in this scope.
    /// </summary>
    public bool HasChanges => _ids.Count > 0;

    /// <summary>
    /// Gets the record of the scope of a tool invocation.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The record; a new, empty one when none is registered.</returns>
    public static ReportToolScopeChanges Get(IServiceProvider services)
        => services.GetService<ReportToolScopeChanges>() ?? new ReportToolScopeChanges();

    /// <summary>
    /// Records that a report, view or group was created, changed or deleted.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    public void Record(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            _ids.Add(id);
        }
    }

    /// <summary>
    /// Determines whether a report, view or group was changed in this scope.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    public bool WasChanged(string id)
        => !string.IsNullOrEmpty(id) && _ids.Contains(id);
}
