using CrestApps.OrchardCore.Asterisk.Web.Models;
using CrestApps.OrchardCore.Asterisk.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Pages;

/// <summary>
/// Renders the live Asterisk dashboard page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly AsteriskDiagnosticsService _asteriskDiagnosticsService;
    private readonly AsteriskWebOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="asteriskDiagnosticsService">The diagnostics service.</param>
    /// <param name="options">The configured defaults.</param>
    public IndexModel(
        AsteriskDiagnosticsService asteriskDiagnosticsService,
        IOptions<AsteriskWebOptions> options)
    {
        _asteriskDiagnosticsService = asteriskDiagnosticsService;
        _options = options.Value;
    }

    /// <summary>
    /// Gets the latest Asterisk diagnostics snapshot.
    /// </summary>
    public AsteriskDiagnosticsSnapshot Diagnostics { get; private set; } = new();

    /// <summary>
    /// Gets the SignalR hub URL used by the live dashboard.
    /// </summary>
    public string DashboardHubUrl => "/hubs/asterisk-dashboard";

    /// <summary>
    /// Gets the JSON snapshot URL used by the live dashboard.
    /// </summary>
    public string DashboardSnapshotUrl => "/api/asterisk/dashboard";

    /// <summary>
    /// Gets the configured dashboard reconciliation interval, in seconds.
    /// </summary>
    public int DashboardRefreshSeconds => Math.Max(1, _options.AsteriskRefreshSeconds);

    /// <summary>
    /// Initializes the page with configuration-backed defaults.
    /// </summary>
    public async Task OnGetAsync()
    {
        await LoadDiagnosticsAsync();
    }

    private async Task LoadDiagnosticsAsync()
    {
        Diagnostics = await _asteriskDiagnosticsService.GetSnapshotAsync(HttpContext.RequestAborted);
    }
}
