using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public static class ChatCompletionServiceExtensions
{
    private static readonly ChatCompletionMessage _titleSystemMessage
        = ChatCompletionMessage.CreateMessage("- Generate a short topic title about the user prompt.\r\n - Response using title case.\r\n - Response must be under 255 characters in length.", "system");

    public static Task<ChatCompletionResponse> GetTitleAsync(this IChatCompletionService chatCompletionService, string prompt, AIChatProfile profile)
    {
        var transcription = new List<ChatCompletionMessage>
        {
            _titleSystemMessage,
            ChatCompletionMessage.CreateMessage(prompt, "user"),
        };

        return chatCompletionService.ChatAsync(transcription, new ChatCompletionContext(profile));
    }
}
