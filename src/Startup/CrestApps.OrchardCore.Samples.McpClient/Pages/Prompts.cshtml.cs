using CrestApps.OrchardCore.Samples.McpClient.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelContextProtocol.Client;

namespace CrestApps.OrchardCore.Samples.McpClient.Pages;

/// <summary>
/// Represents the prompts model.
/// </summary>
public sealed class PromptsModel : PageModel
{
    private readonly McpClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptsModel"/> class.
    /// </summary>
    /// <param name="clientFactory">The client factory.</param>
    public PromptsModel(McpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Gets or sets the prompts.
    /// </summary>
    public IList<McpClientPrompt> Prompts { get; private set; } = [];

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
        await LoadPromptsAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the on post refresh operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        await LoadPromptsAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    /// Asynchronously performs the on post get prompt operation.
    /// </summary>
    /// <param name="promptName">The prompt name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
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
        catch (Exception)
        {
            return new JsonResult(new { error = "An error occurred while getting the prompt." });
        }
    }

    private async Task LoadPromptsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await _clientFactory.CreateAsync(cancellationToken);
            Prompts = await client.ListPromptsAsync(options: null, cancellationToken);
        }
        catch (Exception)
        {
            ErrorMessage ??= "An error occurred while loading prompts.";
            Prompts = [];
        }
    }
}
