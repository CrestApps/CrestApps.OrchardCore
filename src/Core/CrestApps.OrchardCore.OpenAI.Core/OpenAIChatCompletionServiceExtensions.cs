using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI;

public static class OpenAIChatCompletionServiceExtensions
{
    private const string _systemMessage = "- Generate a short topic title about the user prompt.\r\n - Response using title case.\r\n - Response must be under 255 characters in length.";

    public static Task<OpenAIChatCompletionResponse> GetTitleAsync(this IOpenAIChatCompletionService chatCompletionService, string prompt, OpenAIChatProfile profile)
    {
        var transcription = new List<ChatMessage>
        {
            new (ChatRole.User, prompt),
        };

        return chatCompletionService.ChatAsync(transcription, new OpenAIChatCompletionContext(profile)
        {
            SystemMessage = _systemMessage,
            DisableTools = true,
        });
    }
}
