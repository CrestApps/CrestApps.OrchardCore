using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The scripts and styles of the ticket pages: the board, and the board editor's columns.
/// </summary>
internal sealed class TicketsResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The board's script.
    /// </summary>
    public const string BoardScript = "CrestApps.OrchardCore.Tickets.Board";

    /// <summary>
    /// The board's style.
    /// </summary>
    public const string BoardStyle = "CrestApps.OrchardCore.Tickets.Board";

    /// <summary>
    /// The script of the board editor's columns.
    /// </summary>
    public const string BoardEditorScript = "CrestApps.OrchardCore.Tickets.BoardEditor";

    /// <summary>
    /// The ticket list's style.
    /// </summary>
    public const string ListStyle = "CrestApps.OrchardCore.Tickets.List";

    /// <summary>
    /// The style of the ticket type's fields on the ticket page and the new ticket form.
    /// </summary>
    public const string FormStyle = "CrestApps.OrchardCore.Tickets.Form";

    private static readonly ResourceManifest _manifest;

    static TicketsResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(BoardScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Tickets/scripts/ticket-board.min.js",
                "~/CrestApps.OrchardCore.Tickets/scripts/ticket-board.js")
            .SetDependencies("Sortable", "bootstrap")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(BoardStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-board.min.css",
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-board.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(ListStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-list.min.css",
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-list.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(FormStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-form.min.css",
                "~/CrestApps.OrchardCore.Tickets/styles/ticket-form.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript(BoardEditorScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Tickets/scripts/ticket-board-editor.min.js",
                "~/CrestApps.OrchardCore.Tickets/scripts/ticket-board-editor.js")
            .SetDependencies("Sortable")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
