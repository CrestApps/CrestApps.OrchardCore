using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed partial class ResourceTemplatesModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public ResourceTemplatesModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientResourceTemplate> Templates { get; private set; } = [];

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadTemplatesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadTemplatesAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostReadResourceAsync(string resourceUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceUri))
        {
            return new JsonResult(new { error = "Resource URI is required." });
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            var result = await client.ReadResourceAsync(new Uri(resourceUri), options: null, cancellationToken);

            var contents = new List<object>();

            if (result.Contents?.Count > 0)
            {
                foreach (var content in result.Contents)
                {
                    if (content is TextResourceContents textContent)
                    {
                        contents.Add(new { type = "text", mimeType = textContent.MimeType, text = textContent.Text });
                    }
                    else if (content is BlobResourceContents blobContent)
                    {
                        contents.Add(new { type = "blob", mimeType = blobContent.MimeType, length = blobContent.Blob.Length });
                    }
                }
            }

            return new JsonResult(new { contents });
        }
        catch (Exception)
        {
            return new JsonResult(new { error = "An error occurred while reading the resource template." });
        }
    }

    /// <summary>
    /// Extracts parameter names from {param} placeholders in a URI template string.
    /// </summary>
    public static IReadOnlyList<string> ExtractParameters(string uriTemplate)
    {
        if (string.IsNullOrEmpty(uriTemplate))
        {
            return [];
        }

        var matches = ParameterPattern().Matches(uriTemplate);

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

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            Templates = await client.ListResourceTemplatesAsync(options: null, cancellationToken);
        }
        catch (Exception)
        {
            ErrorMessage ??= "An error occurred while loading resource templates.";
            Templates = [];
        }
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ParameterPattern();
}
