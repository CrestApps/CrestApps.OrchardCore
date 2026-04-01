using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed class ResourcesModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public ResourcesModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientResource> Resources { get; private set; } = [];

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
            return new JsonResult(new { error = "An error occurred while reading the resource." });
        }
    }

    private async Task LoadResourcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            var result = await client.ListResourcesAsync(options: null, cancellationToken);
            Resources = result;
        }
        catch (Exception)
        {
            ErrorMessage ??= "An error occurred while loading resources.";
            Resources = [];
        }
    }
}
