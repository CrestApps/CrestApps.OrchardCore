using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed partial class ResourcesModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public ResourcesModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientResource> Resources { get; private set; } = [];

    public string SelectedResourceUri { get; private set; }

    public ReadResourceResult ReadResult { get; private set; }

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadResourcesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadResourcesAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostReadResourceAsync(string resourceUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceUri))
        {
            ErrorMessage = "Resource URI is required.";
            await LoadResourcesAsync(cancellationToken);

            return Page();
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            SelectedResourceUri = resourceUri;
            ReadResult = await client.ReadResourceAsync(new Uri(resourceUri), options: null, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadResourcesAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    /// Extracts parameter names from {param} placeholders in a URI string.
    /// </summary>
    public static IReadOnlyList<string> ExtractParameters(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return [];
        }

        var matches = ParameterPattern().Matches(uri);

        if (matches.Count == 0)
        {
            return [];
        }

        var parameters = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            parameters.Add(match.Groups[1].Value);
        }

        return parameters;
    }

    private async Task LoadResourcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            var result = await client.ListResourcesAsync(options: null, cancellationToken);
            Resources = result;
        }
        catch (Exception ex)
        {
            ErrorMessage ??= ex.Message;
            Resources = [];
        }
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ParameterPattern();
}
