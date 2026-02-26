using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed class PromptsModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public PromptsModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientPrompt> Prompts { get; private set; } = [];

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPromptsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadPromptsAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostGetPromptAsync(string promptName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            return new JsonResult(new { error = "Prompt name is required." });
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            var result = await client.GetPromptAsync(
                promptName,
                new Dictionary<string, object>(),
                options: null,
                cancellationToken);

            var messages = new List<object>();

            if (result.Messages?.Count > 0)
            {
                foreach (var message in result.Messages)
                {
                    messages.Add(new { role = message.Role.ToString(), content = message.Content?.ToString() });
                }
            }

            return new JsonResult(new { description = result.Description, messages });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message });
        }
    }

    private async Task LoadPromptsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            Prompts = await client.ListPromptsAsync(options: null, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage ??= ex.Message;
            Prompts = [];
        }
    }
}
