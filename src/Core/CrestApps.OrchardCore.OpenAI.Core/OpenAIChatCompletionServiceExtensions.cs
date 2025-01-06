using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public static class OpenAIChatCompletionServiceExtensions
{
    private static readonly OpenAIChatCompletionMessage _titleSystemMessage
        = OpenAIChatCompletionMessage.CreateMessage("- Generate a short topic title about the user prompt.\r\n - Response using title case.\r\n - Response must be under 255 characters in length.", OpenAIConstants.Roles.System);

    public static Task<OpenAIChatCompletionResponse> GetTitleAsync(this IOpenAIChatCompletionService chatCompletionService, string prompt, OpenAIChatProfile profile)
    {
        var transcription = new List<OpenAIChatCompletionMessage>
        {
            _titleSystemMessage,
            OpenAIChatCompletionMessage.CreateMessage(prompt, OpenAIConstants.Roles.User),
        };

        return chatCompletionService.ChatAsync(transcription, new OpenAIChatCompletionContext(profile));
    }
}
