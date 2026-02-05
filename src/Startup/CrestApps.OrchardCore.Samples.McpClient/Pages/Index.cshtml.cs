using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

public sealed class IndexModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    public IndexModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public IList<McpClientPrompt> Prompts { get; private set; } = [];

    public IList<McpClientTool> Tools { get; private set; } = [];

    public GetPromptResult PromptResult { get; private set; }

    public string SelectedPromptName { get; private set; }

    public string ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostGetPromptsAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostGetPromptAsync(string promptName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            ErrorMessage = "Prompt name is required.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            SelectedPromptName = promptName;
            PromptResult = await client.GetPromptAsync(
                promptName,
                new Dictionary<string, object>(),
                options: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostGetToolsAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);

            Prompts = await client.ListPromptsAsync(options: null, cancellationToken);
            Tools = await client.ListToolsAsync(options: null, cancellationToken);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Prompts = [];
            Tools = [];
        }
    }
}
