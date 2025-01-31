using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public static class AIChatCompletionServiceExtensions
{
    private const string _systemMessage = "- Generate a short topic title about the user prompt.\r\n - Response using title case.\r\n - Response must be under 255 characters in length.";

    [Obsolete("Instead of this, use a system generated profile.")]
    public static Task<AIChatCompletionResponse> GetTitleAsync(this IAIChatCompletionService chatCompletionService, string prompt, AIChatProfile profile)
    {
        var transcription = new List<ChatMessage>
        {
            new (ChatRole.User, prompt),
        };

        return chatCompletionService.ChatAsync(transcription, new AIChatCompletionContext(profile)
        {
            SystemMessage = _systemMessage,
            DisableTools = true,
        });
    }
}
