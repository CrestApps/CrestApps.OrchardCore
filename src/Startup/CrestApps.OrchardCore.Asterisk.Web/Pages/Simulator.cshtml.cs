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
    /// Initializes the page with configuration-backed defaults.
    /// </summary>
    public void OnGet()
    {
        ApplyDefaults();
    }

    /// <summary>
    /// Runs the configured inbound-call burst.
    /// </summary>
    /// <returns>The current page.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        ApplyDefaults();

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

    private void ApplyDefaults()
    {
        Input.OrchardBaseUrl = string.IsNullOrWhiteSpace(Input.OrchardBaseUrl)
            ? _options.OrchardBaseUrl ?? Input.OrchardBaseUrl
            : Input.OrchardBaseUrl;

        Input.LoginPath = string.IsNullOrWhiteSpace(Input.LoginPath)
            ? _options.LoginPath ?? Input.LoginPath
            : Input.LoginPath;

        Input.InboundPath = string.IsNullOrWhiteSpace(Input.InboundPath)
            ? _options.InboundPath ?? Input.InboundPath
            : Input.InboundPath;

        Input.ProviderName = string.IsNullOrWhiteSpace(Input.ProviderName)
            ? _options.ProviderName ?? Input.ProviderName
            : Input.ProviderName;

        Input.AsteriskDestination = string.IsNullOrWhiteSpace(Input.AsteriskDestination)
            ? _options.AsteriskDestination ?? Input.AsteriskDestination
            : Input.AsteriskDestination;

        Input.ToAddress = string.IsNullOrWhiteSpace(Input.ToAddress)
            ? _options.ToAddress ?? Input.ToAddress
            : Input.ToAddress;

        Input.CallerNumberSeed = string.IsNullOrWhiteSpace(Input.CallerNumberSeed)
            ? _options.CallerNumberSeed ?? Input.CallerNumberSeed
            : Input.CallerNumberSeed;

        Input.CallerNamePrefix = string.IsNullOrWhiteSpace(Input.CallerNamePrefix)
            ? _options.CallerNamePrefix ?? Input.CallerNamePrefix
            : Input.CallerNamePrefix;
    }
}
