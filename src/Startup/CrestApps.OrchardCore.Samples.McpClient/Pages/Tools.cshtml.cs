using System.Text.Json;
using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

/// <summary>
/// Represents the tools model.
/// </summary>
public sealed class ToolsModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolsModel"/> class.
    /// </summary>
    /// <param name="clientFactory">The client factory.</param>
    public ToolsModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Gets or sets the tools.
    /// </summary>
    public IList<McpClientTool> Tools { get; private set; } = [];

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; private set; }

    /// <summary>
    /// Asynchronously performs the on get operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadToolsAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the on post refresh operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadToolsAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    /// Asynchronously performs the on post call tool operation.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
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
        catch (InvalidOperationException ex)
        {
            return new JsonResult(new { error = ex.Message });
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
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            Tools = [];
        }
        catch (Exception)
        {
            ErrorMessage ??= "An error occurred while loading tools.";
            Tools = [];
        }
    }
}
