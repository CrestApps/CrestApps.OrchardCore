using CrestApps.OrchardCore.Asterisk.Web.Models;
using CrestApps.OrchardCore.Asterisk.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Pages;

/// <summary>
/// Renders the inbound call simulator page.
/// </summary>
public sealed class SimulatorModel : PageModel
{
    private readonly InboundCallSimulatorService _simulatorService;
    private readonly AsteriskWebOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulatorModel"/> class.
    /// </summary>
    /// <param name="simulatorService">The simulator service.</param>
    /// <param name="options">The configured defaults.</param>
    public SimulatorModel(
        InboundCallSimulatorService simulatorService,
        IOptions<AsteriskWebOptions> options)
    {
        _simulatorService = simulatorService;
        _options = options.Value;
    }

    /// <summary>
    /// Gets or sets the form input.
    /// </summary>
    [BindProperty]
    public InboundCallSimulationInputModel Input { get; set; } = new();

    /// <summary>
    /// Gets the results of the last simulation burst.
    /// </summary>
    public IReadOnlyList<InboundCallSimulationResult> Results { get; private set; } = [];

    /// <summary>
    /// Gets how many requests succeeded in the last burst.
    /// </summary>
    public int SuccessfulCount => Results.Count(result => result.Succeeded);

    /// <summary>
    /// Gets how many requests reported that they were routed to an agent.
    /// </summary>
    public int RoutedCount => Results.Count(result => result.Routed == true);

    /// <summary>
    /// Gets how many requests are waiting in a Contact Center queue.
    /// </summary>
    public int QueuedCount => Results.Count(result => result.Queued == true);

    /// <summary>
    /// Initializes the page with configuration-backed defaults.
    /// </summary>
    public void OnGet()
    {
        ApplyDefaults(preferConfiguredValues: true);
    }

    /// <summary>
    /// Runs the configured inbound-call burst.
    /// </summary>
    /// <returns>The current page.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        ApplyDefaults(preferConfiguredValues: false);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Results = await _simulatorService.SimulateAsync(Input, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError(string.Empty, $"The simulator could not reach Orchard Core. {ex.Message}");
        }

        return Page();
    }

    private void ApplyDefaults(bool preferConfiguredValues)
    {
        Input.OrchardBaseUrl = ResolveDefault(Input.OrchardBaseUrl, _options.OrchardBaseUrl, preferConfiguredValues);
        Input.LoginPath = ResolveDefault(Input.LoginPath, _options.LoginPath, preferConfiguredValues);
        Input.InboundPath = ResolveDefault(Input.InboundPath, _options.InboundPath, preferConfiguredValues);
        Input.ProviderName = ResolveDefault(Input.ProviderName, _options.ProviderName, preferConfiguredValue: true);
        Input.AsteriskDestination = ResolveDefault(Input.AsteriskDestination, _options.AsteriskDestination, preferConfiguredValues);
        Input.ToAddress = ResolveDefault(Input.ToAddress, _options.ToAddress, preferConfiguredValues);
        Input.CallerNumberSeed = ResolveDefault(Input.CallerNumberSeed, _options.CallerNumberSeed, preferConfiguredValues);
        Input.CallerNamePrefix = ResolveDefault(Input.CallerNamePrefix, _options.CallerNamePrefix, preferConfiguredValues);
    }

    private static string ResolveDefault(string currentValue, string configuredValue, bool preferConfiguredValue)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue) &&
            (preferConfiguredValue || string.IsNullOrWhiteSpace(currentValue)))
        {
            return configuredValue;
        }

        return currentValue;
    }
}
