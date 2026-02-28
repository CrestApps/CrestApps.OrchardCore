using System.Text.Json;
using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed class ToolsModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public ToolsModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientTool> Tools { get; private set; } = [];

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadToolsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadToolsAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostCallToolAsync(string toolName, string arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return new JsonResult(new { error = "Tool name is required." });
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            Dictionary<string, object> args = [];

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments) ?? [];
            }

            var result = await client.CallToolAsync(toolName, args, cancellationToken: cancellationToken);

            var contents = new List<object>();

            if (result.Content?.Count > 0)
            {
                foreach (var content in result.Content)
                {
                    if (content is TextContentBlock textBlock)
                    {
                        contents.Add(new { type = "text", text = textBlock.Text });
                    }
                    else
                    {
                        contents.Add(new { type = "unsupported", contentType = content.Type });
                    }
                }
            }

            return new JsonResult(new { contents, isError = result.IsError });
        }
        catch (Exception)
        {
            return new JsonResult(new { error = "An error occurred while invoking the tool." });
        }
    }

    private async Task LoadToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            Tools = await client.ListToolsAsync(options: null, cancellationToken);
        }
        catch (Exception)
        {
            ErrorMessage ??= "An error occurred while loading tools.";
            Tools = [];
        }
    }
}
