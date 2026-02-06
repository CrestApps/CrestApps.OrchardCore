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

    public string SelectedToolName { get; private set; }

    public CallToolResult CallResult { get; private set; }

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
            ErrorMessage = "Tool name is required.";
            await LoadToolsAsync(cancellationToken);

            return Page();
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            Dictionary<string, object> args = [];

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments) ?? [];
            }

            SelectedToolName = toolName;
            CallResult = await client.CallToolAsync(toolName, args, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadToolsAsync(cancellationToken);

        return Page();
    }

    private async Task LoadToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            Tools = await client.ListToolsAsync(options: null, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage ??= ex.Message;
            Tools = [];
        }
    }
}
